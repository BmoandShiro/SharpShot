# SharpShot Release Builder
# Uses your working OBS Docker.bat workflow but creates a proper release package

Write-Host "Building SharpShot Release Package..." -ForegroundColor Green

# 1. Start Docker environment (like your OBS Docker.bat does)
Write-Host "Step 1: Starting Docker environment..." -ForegroundColor Yellow
docker compose -f docker-compose.dev.yml down
docker compose -f docker-compose.dev.yml up --build -d

# Wait for container
Start-Sleep -Seconds 5

# 2. Build in Docker (like your obs-docker.ps1 does)
Write-Host "Step 2: Building SharpShot in Docker..." -ForegroundColor Yellow
docker exec sharpshot-development dotnet restore
docker exec sharpshot-development dotnet build --configuration Release -p:Platform=x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# 3. Create release folder
$releaseFolder = "SharpShot-Release-v1.0"
if (Test-Path $releaseFolder) { Remove-Item -Recurse -Force $releaseFolder }
New-Item -ItemType Directory -Path $releaseFolder | Out-Null

# 4. Copy SharpShot files (from the working build location)
Write-Host "Step 3: Copying SharpShot files..." -ForegroundColor Yellow
$sourceDir = "bin\x64\Release\net8.0-windows\win-x64"
Copy-Item -Path "$sourceDir\*" -Destination $releaseFolder -Recurse -Force

# 5. Copy OBS Studio (from the working bundled location)
Write-Host "Step 4: Copying OBS Studio..." -ForegroundColor Yellow
$obsSourceDir = "bin\Release\net8.0-windows\x64\OBS-Studio"
if (Test-Path $obsSourceDir) {
    Copy-Item -Path $obsSourceDir -Destination "$releaseFolder\OBS-Studio" -Recurse -Force
    Write-Host "OBS Studio bundled successfully!" -ForegroundColor Green
} else {
    Write-Host "Warning: OBS Studio not found in expected location" -ForegroundColor Yellow
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
echo    SharpShot v1.0 - Release Package
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
$zipName = "SharpShot-Release-v1.0.zip"
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
Write-Host "- Launchers and documentation" -ForegroundColor White
Write-Host "- License files (GPL v2, FFmpeg, OBS Studio)" -ForegroundColor White
Write-Host ""
Write-Host "Test the release package by running:" -ForegroundColor Yellow
Write-Host "  $releaseFolder\Run SharpShot.bat" -ForegroundColor Cyan

# Ask if user wants to open the release folder
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
