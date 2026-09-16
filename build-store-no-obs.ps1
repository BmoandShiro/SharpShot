# SharpShot Microsoft Store / no-OBS build
# Builds a portable ZIP and an MSIX package WITHOUT bundling OBS Studio.
# Publish uses StoreBuild=true so the GitHub updater is compiled out.
# Partner Center re-signs the .msix; this script does not require a local cert.
# Does not modify build-release.ps1 or other existing packaging scripts.
# OBS can be linked at runtime from Settings or the custom/OBS launcher buttons.

param(
    [string]$Configuration = "Release",
    [switch]$SkipMsix,
    [switch]$SkipZip,
    [switch]$NoPrompt
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "========================================" -ForegroundColor Green
Write-Host " SharpShot Store Build (No OBS Bundle)" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

# Sync version from Version file
& "$PSScriptRoot\Apply-Version.ps1" -ProjectDir $PSScriptRoot
$version = (Get-Content "$PSScriptRoot\Version" -Raw).Trim()
$parts = $version.Split('.')
if ($parts.Length -eq 2) { $version = "$version.0.0" }
elseif ($parts.Length -eq 3) { $version = "$version.0" }

$releaseFolder = "SharpShot-Store-v$version"
$zipName = "SharpShot-Store-v$version.zip"
# Platform=x64 writes to bin\x64\... even when some older scripts assumed bin\Release\...
$publishDir = "bin\x64\$Configuration\net8.0-windows\win-x64\publish"
$msixContentDir = "bin\$Configuration\msix-content-no-obs"
$msixFile = "bin\$Configuration\SharpShot-Store-v$version.msix"

Write-Host "Version: $version" -ForegroundColor Cyan

# Resolve MakeAppx (optional if -SkipMsix)
function Find-MakeAppx {
    $candidates = @(
        "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe",
        "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe",
        "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    $kitRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (Test-Path $kitRoot) {
        $found = Get-ChildItem -Path $kitRoot -Recurse -Filter "makeappx.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\makeappx\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

Write-Host "`nStep 1: Publish (StoreBuild=true, ExcludeObsBundle=true)..." -ForegroundColor Yellow
dotnet publish SharpShot.csproj `
    --configuration $Configuration `
    -p:Platform=x64 `
    -p:StoreBuild=true `
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

# Portable folder
Write-Host "`nStep 2: Creating portable folder (no OBS)..." -ForegroundColor Yellow
if (Test-Path $releaseFolder) { Remove-Item -Recurse -Force $releaseFolder }
New-Item -ItemType Directory -Path $releaseFolder | Out-Null

Copy-Item -Path "$publishDir\*" -Destination $releaseFolder -Recurse -Force

# Remove any OBS that might have been left in publish from prior builds
$obsInRelease = Join-Path $releaseFolder "OBS-Studio"
if (Test-Path $obsInRelease) {
    Remove-Item -Recurse -Force $obsInRelease
    Write-Host "Removed OBS-Studio from portable output." -ForegroundColor Gray
}

# tessdata
if (Test-Path "tessdata") {
    $tessDest = Join-Path $releaseFolder "tessdata"
    if (!(Test-Path $tessDest)) { New-Item -ItemType Directory -Path $tessDest -Force | Out-Null }
    Copy-Item -Path "tessdata\*" -Destination $tessDest -Recurse -Force
}

# FFmpeg
if (Test-Path "ffmpeg") {
    Copy-Item -Path "ffmpeg" -Destination "$releaseFolder\ffmpeg" -Recurse -Force
    Write-Host "FFmpeg copied." -ForegroundColor Green
}

# Docs / licenses (skip OBS-LICENSE / OBS_INTEGRATION for store package)
if (Test-Path "README.md") { Copy-Item "README.md" $releaseFolder -Force }
if (Test-Path "LICENSE") { Copy-Item "LICENSE" $releaseFolder -Force }
if (Test-Path "PRIVACY.md") { Copy-Item "PRIVACY.md" $releaseFolder -Force }
if (Test-Path "FFmpeg-LICENSE.txt") { Copy-Item "FFmpeg-LICENSE.txt" $releaseFolder -Force }

$launcherContent = @"
@echo off
echo ========================================
echo    SharpShot v$version (Store / No OBS)
echo ========================================
echo.
echo Starting SharpShot...
echo Link OBS from Settings - Recording if needed.
echo.
start "" "SharpShot.exe"
"@
Set-Content -Path "$releaseFolder\Run SharpShot.bat" -Value $launcherContent

if (-not $SkipZip) {
    Write-Host "`nStep 3: Creating ZIP..." -ForegroundColor Yellow
    if (Test-Path $zipName) { Remove-Item $zipName -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($releaseFolder, $zipName)
    Write-Host "ZIP: $zipName" -ForegroundColor Cyan
}

if (-not $SkipMsix) {
    Write-Host "`nStep 4: Creating MSIX (no OBS)..." -ForegroundColor Yellow
    $makeAppx = Find-MakeAppx
    if (-not $makeAppx) {
        Write-Host "MakeAppx.exe not found. Skipping MSIX. Install Windows SDK or use -SkipMsix." -ForegroundColor Yellow
    }
    else {
        if (Test-Path $msixContentDir) { Remove-Item -Recurse -Force $msixContentDir }
        New-Item -ItemType Directory -Path $msixContentDir -Force | Out-Null

        Copy-Item -Path "$publishDir\*" -Destination $msixContentDir -Recurse -Force
        $obsInMsix = Join-Path $msixContentDir "OBS-Studio"
        if (Test-Path $obsInMsix) { Remove-Item -Recurse -Force $obsInMsix }

        if (Test-Path "tessdata") {
            $tessMsix = Join-Path $msixContentDir "tessdata"
            if (!(Test-Path $tessMsix)) { New-Item -ItemType Directory -Path $tessMsix -Force | Out-Null }
            Copy-Item -Path "tessdata\*" -Destination $tessMsix -Recurse -Force
        }
        if (Test-Path "ffmpeg") {
            Copy-Item -Path "ffmpeg" -Destination (Join-Path $msixContentDir "ffmpeg") -Recurse -Force
        }

        $requiredAssets = @(
            "Assets\StoreLogo.png",
            "Assets\Square150x150Logo.png",
            "Assets\Square44x44Logo.png",
            "Assets\Wide310x150Logo.png",
            "Assets\SplashScreen.png"
        )
        foreach ($asset in $requiredAssets) {
            if (-not (Test-Path $asset)) {
                Write-Host "Missing Store asset: $asset" -ForegroundColor Red
                exit 1
            }
        }

        # Manifest must be named AppxManifest.xml for MakeAppx. Partner Center re-signs this package.
        Copy-Item "Package.appxmanifest" -Destination (Join-Path $msixContentDir "AppxManifest.xml") -Force
        if (Test-Path "PRIVACY.md") { Copy-Item "PRIVACY.md" $msixContentDir -Force }

        if (Test-Path "Assets") {
            Copy-Item "Assets" -Destination $msixContentDir -Recurse -Force
        }

        if (Test-Path $msixFile) { Remove-Item $msixFile -Force }
        & $makeAppx pack /d $msixContentDir /p $msixFile /o
        if ($LASTEXITCODE -eq 0) {
            Write-Host "MSIX: $msixFile" -ForegroundColor Cyan
        }
        else {
            Write-Host "MakeAppx failed (exit $LASTEXITCODE)." -ForegroundColor Red
            exit 1
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " STORE / NO-OBS BUILD COMPLETE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "Portable folder: $releaseFolder" -ForegroundColor Cyan
if (-not $SkipZip) { Write-Host "ZIP: $zipName" -ForegroundColor Cyan }
if ((-not $SkipMsix) -and (Test-Path $msixFile)) { Write-Host "MSIX: $msixFile" -ForegroundColor Cyan }
Write-Host ""
Write-Host "OBS is not bundled. Users link OBS in Settings - Recording." -ForegroundColor White

if (-not $NoPrompt) {
    $openFolder = Read-Host "Open portable folder? (y/n)"
    if ($openFolder -eq "y" -or $openFolder -eq "Y") {
        explorer $releaseFolder
    }
}
