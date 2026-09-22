# SharpShot Steam depot builder
# Publishes a folder Steam can upload as a content depot.
# Does not bundle OBS, does not build MSI/NSIS/MSIX, and compiles with SteamBuild=true
# so the GitHub updater is compiled out of the binary.

param(
    [string]$Configuration = "Release",
    [switch]$NoPrompt
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "========================================" -ForegroundColor Green
Write-Host " SharpShot Steam Depot Build" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

& "$PSScriptRoot\Apply-Version.ps1" -ProjectDir $PSScriptRoot
$version = (Get-Content "$PSScriptRoot\Version" -Raw).Trim()
$parts = $version.Split('.')
if ($parts.Length -eq 2) { $version = "$version.0.0" }
elseif ($parts.Length -eq 3) { $version = "$version.0" }

$depotFolder = "steam-depot"
# Platform=x64 writes to bin\x64\... even when some older scripts assumed bin\Release\...
$publishDir = "bin\x64\$Configuration\net8.0-windows\win-x64\publish"

Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host "Launch executable for Steam: SharpShot.exe" -ForegroundColor Cyan

Write-Host "`nPublishing (SteamBuild=true, ExcludeObsBundle=true)..." -ForegroundColor Yellow
dotnet publish SharpShot.csproj `
    --configuration $Configuration `
    -p:Platform=x64 `
    -p:SteamBuild=true `
    -p:ExcludeObsBundle=true `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -p:RuntimeIdentifier=win-x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path "$publishDir\SharpShot.exe")) {
    $fallback = "bin\$Configuration\net8.0-windows\win-x64\publish"
    if (Test-Path "$fallback\SharpShot.exe") {
        $publishDir = $fallback
    }
    else {
        Write-Host "SharpShot.exe not found at $publishDir" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`nCreating steam-depot folder..." -ForegroundColor Yellow
if (Test-Path $depotFolder) { Remove-Item -Recurse -Force $depotFolder }
New-Item -ItemType Directory -Path $depotFolder | Out-Null
Copy-Item -Path "$publishDir\*" -Destination $depotFolder -Recurse -Force

if (Test-Path "tessdata") {
    $tessInPublish = Join-Path $publishDir "tessdata"
    if (!(Test-Path $tessInPublish)) { New-Item -ItemType Directory -Path $tessInPublish -Force | Out-Null }
    Copy-Item -Path "tessdata\*" -Destination $tessInPublish -Recurse -Force
}

# PublishSingleFile leaves x64\tesseract natives out of the publish folder.
$nativeCandidates = @(
    "bin\x64\$Configuration\net8.0-windows\win-x64\x64",
    (Join-Path $env:USERPROFILE ".nuget\packages\tesseract\5.2.0\x64")
)
$srcX64 = $nativeCandidates | Where-Object { Test-Path (Join-Path $_ "tesseract50.dll") } | Select-Object -First 1
if ($srcX64) {
    foreach ($root in @($publishDir, $depotFolder)) {
        if (-not (Test-Path $root)) { continue }
        $destX64 = Join-Path $root "x64"
        New-Item -ItemType Directory -Path $destX64 -Force | Out-Null
        Copy-Item -Path (Join-Path $srcX64 "*") -Destination $destX64 -Force
    }
    Write-Host "Tesseract natives copied for Steam depot." -ForegroundColor Green
}


$obsInDepot = Join-Path $depotFolder "OBS-Studio"
if (Test-Path $obsInDepot) {
    Remove-Item -Recurse -Force $obsInDepot
    Write-Host "Removed leftover OBS-Studio from depot." -ForegroundColor Gray
}

if (Test-Path "tessdata") {
    $tessDest = Join-Path $depotFolder "tessdata"
    if (!(Test-Path $tessDest)) { New-Item -ItemType Directory -Path $tessDest -Force | Out-Null }
    Copy-Item -Path "tessdata\*" -Destination $tessDest -Recurse -Force
}

if (Test-Path "ffmpeg") {
    Copy-Item -Path "ffmpeg" -Destination "$depotFolder\ffmpeg" -Recurse -Force
    Write-Host "FFmpeg copied." -ForegroundColor Green
}
else {
    Write-Host "Warning: ffmpeg folder not found. Recording will not work until it is added." -ForegroundColor Yellow
}

if (Test-Path "LICENSE") { Copy-Item "LICENSE" $depotFolder -Force }
if (Test-Path "PRIVACY.md") { Copy-Item "PRIVACY.md" $depotFolder -Force }
if (Test-Path "FFmpeg-LICENSE.txt") { Copy-Item "FFmpeg-LICENSE.txt" $depotFolder -Force }
if (Test-Path "Version") { Copy-Item "Version" $depotFolder -Force }

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " STEAM DEPOT READY" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "Folder: $depotFolder" -ForegroundColor Cyan
Write-Host "Set the Steam launch option to SharpShot.exe" -ForegroundColor White
Write-Host "OBS is not bundled. Users can link an installed OBS (including the Steam OBS app)." -ForegroundColor White

if (-not $NoPrompt) {
    $openFolder = Read-Host "Open steam-depot folder? (y/n)"
    if ($openFolder -eq "y" -or $openFolder -eq "Y") {
        explorer $depotFolder
    }
}
