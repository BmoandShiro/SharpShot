# SharpShot Release Builder
# Uses your working OBS Docker.bat workflow but creates a proper release package
# Version is read from the Version file; Apply-Version.ps1 syncs it to all project files before building.
param(
    [switch]$NoPrompt  # When set, skips "open folder?" and "stop Docker?" prompts (for 3xbuild.bat)
)

# Sync version from Version file to csproj, installers, etc., then read it for this script
& "$PSScriptRoot\Apply-Version.ps1" -ProjectDir $PSScriptRoot
$version = (Get-Content "$PSScriptRoot\Version" -Raw).Trim()
$parts = $version.Split('.')
if ($parts.Length -eq 2) { $version = "$version.0.0" }
elseif ($parts.Length -eq 3) { $version = "$version.0" }
$releaseFolder = "SharpShot-Release-v$version"
$zipName = "SharpShot-Release-v$version.zip"

Write-Host "Building SharpShot Release Package (v$version)..." -ForegroundColor Green

$sourceDir = "bin\x64\Release\net8.0-windows\win-x64"
$dockerSucceeded = $false

# Try Docker first (if available)
try {
    $null = docker version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Step 1: Starting Docker environment..." -ForegroundColor Yellow
        docker compose -f docker-compose.dev.yml down 2>&1 | Out-Null
        docker compose -f docker-compose.dev.yml up --build -d 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Docker compose up failed" }
        Start-Sleep -Seconds 5
        Write-Host "Step 2: Building SharpShot in Docker..." -ForegroundColor Yellow
        docker exec sharpshot-development dotnet restore
        docker exec sharpshot-development dotnet build --configuration Release -p:Platform=x64
        if ($LASTEXITCODE -eq 0 -and (Test-Path "$sourceDir\SharpShot.exe")) { $dockerSucceeded = $true }
    }
} catch {
    # Docker not available or failed
}

if (-not $dockerSucceeded) {
    Write-Host "Docker not used or failed. Building locally..." -ForegroundColor Yellow
    dotnet restore
    dotnet build --configuration Release -p:Platform=x64
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
    if (-not (Test-Path "$sourceDir\SharpShot.exe")) {
        Write-Host "Build succeeded but SharpShot.exe not found at $sourceDir" -ForegroundColor Red
        exit 1
    }
    Write-Host "Local build succeeded." -ForegroundColor Green
}

# 3. Create release folder
if (Test-Path $releaseFolder) { Remove-Item -Recurse -Force $releaseFolder }
New-Item -ItemType Directory -Path $releaseFolder | Out-Null

# 4. Copy SharpShot files (from the working build location)
Write-Host "Step 3: Copying SharpShot files..." -ForegroundColor Yellow
Copy-Item -Path "$sourceDir\*" -Destination $releaseFolder -Recurse -Force

# 4b. Ensure tessdata (OCR language data) is in the release folder (build may not include it in Docker)
Write-Host "Step 3b: Copying tessdata (OCR languages)..." -ForegroundColor Yellow
if (Test-Path "tessdata") {
    $tessDest = Join-Path $releaseFolder "tessdata"
    if (!(Test-Path $tessDest)) { New-Item -ItemType Directory -Path $tessDest -Force | Out-Null }
    Copy-Item -Path "tessdata\*" -Destination $tessDest -Recurse -Force
    Write-Host "tessdata folder copied successfully!" -ForegroundColor Green
} else {
    Write-Host "No tessdata folder in project (OCR will be unavailable unless added to release later)." -ForegroundColor Gray
}
Get-ChildItem -Path "." -Filter "*.traineddata" -File -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $releaseFolder -Force
    Write-Host "Copied $($_.Name) to release folder" -ForegroundColor Green
}

# 5. Copy OBS Studio (from bin bundle or project root)
Write-Host "Step 4: Copying OBS Studio..." -ForegroundColor Yellow
$obsSourceDir = "bin\Release\net8.0-windows\x64\OBS-Studio"
if (Test-Path $obsSourceDir) {
    Copy-Item -Path $obsSourceDir -Destination "$releaseFolder\OBS-Studio" -Recurse -Force
    Write-Host "OBS Studio bundled successfully!" -ForegroundColor Green
} elseif (Test-Path "OBS-Studio") {
    Copy-Item -Path "OBS-Studio" -Destination "$releaseFolder\OBS-Studio" -Recurse -Force
    Write-Host "OBS Studio copied from project folder." -ForegroundColor Green
} else {
    Write-Host "Warning: OBS Studio not found. Skip if not needed." -ForegroundColor Yellow
}

# 6. Copy FFmpeg if exists
Write-Host "Step 5: Copying FFmpeg..." -ForegroundColor Yellow
if (Test-Path "ffmpeg") {
    Copy-Item -Path "ffmpeg" -Destination "$releaseFolder\ffmpeg" -Recurse -Force
    Write-Host "FFmpeg bundled successfully!" -ForegroundColor Green
}

# 7. Copy documentation and license files
Write-Host "Step 6: Copying documentation and license files..." -ForegroundColor Yellow
if (Test-Path "README.md") { Copy-Item -Path "README.md" -Destination $releaseFolder -Force }
if (Test-Path "LICENSE") { Copy-Item -Path "LICENSE" -Destination $releaseFolder -Force }
if (Test-Path "OBS_INTEGRATION.md") { Copy-Item -Path "OBS_INTEGRATION.md" -Destination $releaseFolder -Force }

# Copy license files for third-party components
Write-Host "Step 6a: Copying third-party license files..." -ForegroundColor Yellow
if (Test-Path "FFmpeg-LICENSE.txt") { Copy-Item -Path "FFmpeg-LICENSE.txt" -Destination $releaseFolder -Force }
if (Test-Path "OBS-LICENSE.txt") { Copy-Item -Path "OBS-LICENSE.txt" -Destination $releaseFolder -Force }
Write-Host "Third-party license files copied successfully!" -ForegroundColor Green

# 8. Create launcher scripts
Write-Host "Step 7: Creating launchers..." -ForegroundColor Yellow

# Main launcher
$launcherContent = @"
@echo off
echo ========================================
echo    SharpShot v$version - Release Package
echo ========================================
echo.
echo Starting SharpShot...
echo.
start "" "SharpShot.exe"
"@
Set-Content -Path "$releaseFolder\Run SharpShot.bat" -Value $launcherContent

# OBS launcher
if (Test-Path "$releaseFolder\OBS-Studio") {
    $obsLauncherContent = @"
@echo off
echo ========================================
echo    OBS Studio Launcher
echo ========================================
echo.
echo Starting OBS Studio...
echo.
start "" "OBS-Studio\bin\64bit\obs64.exe"
"@
    Set-Content -Path "$releaseFolder\Run OBS Studio.bat" -Value $obsLauncherContent
}

# 9. Create ZIP package
Write-Host "Step 8: Creating ZIP package..." -ForegroundColor Yellow
if (Test-Path $zipName) { Remove-Item $zipName }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($releaseFolder, $zipName)

# 10. Final summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "    RELEASE BUILD COMPLETE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Release package created:" -ForegroundColor White
Write-Host "- Folder: $releaseFolder" -ForegroundColor Cyan
Write-Host "- ZIP: $zipName" -ForegroundColor Cyan
Write-Host ""
Write-Host "Package includes:" -ForegroundColor White
Write-Host "- SharpShot.exe (standalone)" -ForegroundColor White
Write-Host "- OBS Studio (complete)" -ForegroundColor White
Write-Host "- FFmpeg (if available)" -ForegroundColor White
Write-Host "- tessdata (OCR languages, if present in project)" -ForegroundColor White
Write-Host "- Launchers and documentation" -ForegroundColor White
Write-Host "- License files (GPL v2, FFmpeg, OBS Studio)" -ForegroundColor White
Write-Host ""
Write-Host "Test the release package by running:" -ForegroundColor Yellow
Write-Host "  $releaseFolder\Run SharpShot.bat" -ForegroundColor Cyan

# Ask if user wants to open the release folder (skip when -NoPrompt for 3xbuild)
if (-not $NoPrompt) {
    $openFolder = Read-Host "Do you want to open the release folder? (y/n)"
    if ($openFolder -eq "y" -or $openFolder -eq "Y") {
        explorer $releaseFolder
    }

    # Ask if user wants to stop Docker
    $stopDocker = Read-Host "Stop Docker environment? (y/n)"
    if ($stopDocker -eq "y" -or $stopDocker -eq "Y") {
        docker compose -f docker-compose.dev.yml down
        Write-Host "Docker stopped." -ForegroundColor Green
    }
}
