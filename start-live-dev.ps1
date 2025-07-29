# SharpShot Live Development Environment
# This script provides live code updates using dotnet watch in Docker

Write-Host "=== SharpShot Live Development Environment ===" -ForegroundColor Cyan

# Check if Docker is running
try {
    docker version | Out-Null
    Write-Host "Docker is running!" -ForegroundColor Green
} catch {
    Write-Host "Error: Docker is not running!" -ForegroundColor Red
    Write-Host "Please start Docker Desktop and try again." -ForegroundColor Yellow
    exit 1
}

# Navigate to project directory
Set-Location $PSScriptRoot

# Check if FFmpeg is available on Windows host
Write-Host "Checking FFmpeg availability..." -ForegroundColor Yellow
$ffmpegInstalled = $false

try {
    $ffmpegTest = & ffmpeg -version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "FFmpeg found on Windows host!" -ForegroundColor Green
        $ffmpegInstalled = $true
    } else {
        throw "FFmpeg not found in PATH"
    }
} catch {
    # Check if FFmpeg is in the bundled location
    $bundledFfmpegPath = Join-Path $PSScriptRoot "ffmpeg\bin\ffmpeg.exe"
    if (Test-Path $bundledFfmpegPath) {
        Write-Host "FFmpeg found in bundled location!" -ForegroundColor Green
        $ffmpegInstalled = $true
    } else {
        Write-Host "FFmpeg not found. Installing..." -ForegroundColor Yellow
        
        # Run the FFmpeg setup script
        if (Test-Path "setup-ffmpeg.ps1") {
            & "$PSScriptRoot\setup-ffmpeg.ps1"
            
            # Check if installation was successful
            if (Test-Path $bundledFfmpegPath) {
                Write-Host "FFmpeg installed successfully!" -ForegroundColor Green
                $ffmpegInstalled = $true
            } else {
                Write-Host "Warning: FFmpeg installation may have failed." -ForegroundColor Yellow
                Write-Host "Please install FFmpeg manually from: https://ffmpeg.org/download.html" -ForegroundColor Yellow
            }
        } else {
            Write-Host "Warning: setup-ffmpeg.ps1 not found. Please install FFmpeg manually." -ForegroundColor Yellow
            Write-Host "Download from: https://ffmpeg.org/download.html" -ForegroundColor Yellow
        }
    }
}

if (-not $ffmpegInstalled) {
    Write-Host "Warning: FFmpeg is not available. Video recording may not work." -ForegroundColor Yellow
}

# Stop any existing containers
Write-Host "Stopping any existing containers..." -ForegroundColor Yellow
docker compose -f docker-compose.live-dev.yml down

# Build and start the live development environment
Write-Host "Building live development environment..." -ForegroundColor Yellow
docker compose -f docker-compose.live-dev.yml up --build -d

if ($LASTEXITCODE -eq 0) {
    Write-Host "Live development environment started successfully!" -ForegroundColor Green
    
    # Wait a moment for container to be ready
    Start-Sleep -Seconds 3
    
    Write-Host ""
    Write-Host "=== Live Development Workflow ===" -ForegroundColor Cyan
    Write-Host "✅ Docker container is running with dotnet watch" -ForegroundColor Green
    Write-Host "✅ Code changes will automatically trigger rebuilds" -ForegroundColor Green
    Write-Host "✅ Build artifacts are shared between container and host" -ForegroundColor Green
    Write-Host ""
    Write-Host "=== How to Use ===" -ForegroundColor Cyan
    Write-Host "1. Make changes to your code files" -ForegroundColor Gray
    Write-Host "2. Docker will automatically rebuild the project" -ForegroundColor Gray
    Write-Host "3. Run the application locally: dotnet run" -ForegroundColor Gray
    Write-Host "4. Or use: dotnet run --project SharpShot.csproj" -ForegroundColor Gray
    Write-Host ""
    Write-Host "=== Quick Commands ===" -ForegroundColor Cyan
    Write-Host "Run locally: dotnet run" -ForegroundColor Gray
    Write-Host "Build manually: docker exec sharpshot-live-development dotnet build" -ForegroundColor Gray
    Write-Host "View logs: docker logs -f sharpshot-live-development" -ForegroundColor Gray
    Write-Host "Stop Docker: docker compose -f docker-compose.live-dev.yml down" -ForegroundColor Gray
    Write-Host ""
    Write-Host "=== Benefits ===" -ForegroundColor Cyan
    Write-Host "✅ Live code updates with dotnet watch" -ForegroundColor Green
    Write-Host "✅ No need to rebuild Docker image for code changes" -ForegroundColor Green
    Write-Host "✅ Consistent .NET environment" -ForegroundColor Green
    Write-Host "✅ Shared build cache for faster builds" -ForegroundColor Green
    Write-Host ""
    Write-Host "=== Starting Application ===" -ForegroundColor Cyan
    Write-Host "The Docker container is now watching for changes..." -ForegroundColor Yellow
    Write-Host "You can now run the application locally:" -ForegroundColor Yellow
    Write-Host "dotnet run" -ForegroundColor White
    
    # Start asset watcher in background
    Write-Host "Starting asset file watcher..." -ForegroundColor Yellow
    Start-Job -ScriptBlock {
        Set-Location $using:PSScriptRoot
        & "$PSScriptRoot\watch-assets.ps1"
    } | Out-Null
    Write-Host "✅ Asset watcher started in background" -ForegroundColor Green
    
    # Start XAML file watcher in background
    Write-Host "Starting XAML file watcher..." -ForegroundColor Yellow
    Start-Job -ScriptBlock {
        Set-Location $using:PSScriptRoot
        & "$PSScriptRoot\force-refresh.ps1" watch
    } | Out-Null
    Write-Host "✅ XAML watcher started in background" -ForegroundColor Green
    
    # Optionally start the application
    $startApp = Read-Host "Would you like to start the application now? (y/n)"
    if ($startApp -eq "y" -or $startApp -eq "Y") {
        Write-Host "Starting SharpShot..." -ForegroundColor Yellow
        dotnet run
    }
    
} else {
    Write-Host "Failed to start live development environment!" -ForegroundColor Red
}

Write-Host "=== Script Complete ===" -ForegroundColor Cyan 