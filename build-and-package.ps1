# Build and Package SharpShot
# This script builds the application and creates a distribution package

Write-Host "Building SharpShot..." -ForegroundColor Green

# Check if FFmpeg is set up with WASAPI support
$ffmpegPath = "ffmpeg\bin\ffmpeg.exe"
if (!(Test-Path $ffmpegPath)) {
    Write-Host "FFmpeg not found. Setting up FFmpeg with WASAPI support..." -ForegroundColor Yellow
    & "$PSScriptRoot\setup-windows-ffmpeg.ps1"
} else {
    # Check if current FFmpeg supports WASAPI
    try {
        # Use a simpler approach with Invoke-Expression
        $output = & $ffmpegPath -formats 2>&1
        if ($LASTEXITCODE -eq 0 -and $output -notmatch "wasapi") {
            Write-Host "Current FFmpeg does not support WASAPI. Updating..." -ForegroundColor Yellow
            & "$PSScriptRoot\setup-windows-ffmpeg.ps1"
        } else {
            Write-Host "FFmpeg with WASAPI support found!" -ForegroundColor Green
        }
    } catch {
        Write-Host "Warning: Could not check FFmpeg WASAPI support. Video recording may not work." -ForegroundColor Yellow
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