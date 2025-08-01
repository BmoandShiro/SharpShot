# Setup Windows FFmpeg with WASAPI Support
# This script ensures the correct Windows FFmpeg build is installed

Write-Host "Setting up Windows FFmpeg with WASAPI support..." -ForegroundColor Green

# Check if we're in the right directory
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectDir

Write-Host "Current directory: $projectDir" -ForegroundColor Yellow

# Check if FFmpeg exists and supports WASAPI
$ffmpegPath = "ffmpeg\bin\ffmpeg.exe"
if (Test-Path $ffmpegPath) {
    Write-Host "FFmpeg found. Checking if it supports WASAPI..." -ForegroundColor Yellow
    
    try {
        # Use a simpler approach with Invoke-Expression
        $output = & $ffmpegPath -formats 2>&1
        if ($LASTEXITCODE -eq 0) {
            if ($output -match "wasapi") {
                Write-Host "SUCCESS: FFmpeg supports WASAPI!" -ForegroundColor Green
                Write-Host "FFmpeg is ready for audio recording with system audio capture." -ForegroundColor Green
                exit 0
            } else {
                Write-Host "Current FFmpeg does not support WASAPI. Updating..." -ForegroundColor Yellow
                Write-Host "FFmpeg output: $output" -ForegroundColor Gray
            }
        } else {
            Write-Host "FFmpeg check failed with exit code: $LASTEXITCODE" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "Error checking FFmpeg: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "FFmpeg not found. Installing..." -ForegroundColor Yellow
}

# Run the setup script to install/update FFmpeg
Write-Host "Running FFmpeg setup..." -ForegroundColor Yellow
& "$PSScriptRoot\setup-ffmpeg.ps1"

# Verify the installation
if (Test-Path $ffmpegPath) {
    Write-Host "Testing new FFmpeg installation..." -ForegroundColor Yellow
    
    try {
        # Use a simpler approach with Invoke-Expression
        $output = & $ffmpegPath -formats 2>&1
        if ($LASTEXITCODE -eq 0) {
            if ($output -match "wasapi") {
                Write-Host "SUCCESS: FFmpeg with WASAPI support installed!" -ForegroundColor Green
                Write-Host "You can now use system audio recording in SharpShot." -ForegroundColor Green
            } else {
                Write-Host "WARNING: FFmpeg may not support WASAPI. Check the output:" -ForegroundColor Yellow
                Write-Host $output -ForegroundColor Gray
            }
        } else {
            Write-Host "FFmpeg test failed with exit code: $LASTEXITCODE" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "Error testing FFmpeg: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "ERROR: FFmpeg installation failed!" -ForegroundColor Red
    Write-Host "Please run setup-ffmpeg.ps1 manually." -ForegroundColor Yellow
}

Write-Host "Windows FFmpeg setup completed." -ForegroundColor Green 