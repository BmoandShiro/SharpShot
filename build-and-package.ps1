# Build and Package SharpShot
# This script builds the application and creates a distribution package

Write-Host "Building SharpShot..." -ForegroundColor Green

# Check if FFmpeg is set up
$ffmpegPath = "ffmpeg\bin\ffmpeg.exe"
if (!(Test-Path $ffmpegPath)) {
    Write-Host "FFmpeg not found. Setting up FFmpeg..." -ForegroundColor Yellow
    & "$PSScriptRoot\setup-ffmpeg.ps1"
    
    if (!(Test-Path $ffmpegPath)) {
        Write-Host "Warning: FFmpeg setup failed. Video recording may not work." -ForegroundColor Yellow
        Write-Host "Please run setup-ffmpeg.ps1 manually or download FFmpeg from https://ffmpeg.org/" -ForegroundColor Yellow
    }
}

# Build the application
Write-Host "Building application..." -ForegroundColor Yellow
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Publish the application
Write-Host "Publishing application..." -ForegroundColor Yellow
dotnet publish --configuration Release --output "publish" --runtime win-x64 --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# Copy FFmpeg to publish directory
if (Test-Path "ffmpeg") {
    Write-Host "Copying FFmpeg to publish directory..." -ForegroundColor Yellow
    Copy-Item -Path "ffmpeg" -Destination "publish\ffmpeg" -Recurse -Force
}

# Create distribution package
Write-Host "Creating distribution package..." -ForegroundColor Yellow
$version = "1.0.0"
$packageName = "SharpShot-v$version.zip"

if (Test-Path $packageName) {
    Remove-Item $packageName -Force
}

Compress-Archive -Path "publish\*" -DestinationPath $packageName

Write-Host "Build and package completed successfully!" -ForegroundColor Green
Write-Host "Package created: $packageName" -ForegroundColor Green 