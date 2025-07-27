# SharpShot Development Environment
# This script uses Docker for consistent builds and automatically runs the program

Write-Host "=== SharpShot Development Environment ===" -ForegroundColor Cyan

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
docker compose -f docker-compose.dev.yml down

# Build and start the development environment
Write-Host "Building development environment..." -ForegroundColor Yellow
docker compose -f docker-compose.dev.yml up --build -d

if ($LASTEXITCODE -eq 0) {
    Write-Host "Development environment started successfully!" -ForegroundColor Green
    
    # Wait a moment for container to be ready
    Start-Sleep -Seconds 3
    
    # Build in Docker (consistent environment)
    Write-Host "Building SharpShot in Docker..." -ForegroundColor Yellow
    docker exec sharpshot-development dotnet build
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build successful! Starting SharpShot..." -ForegroundColor Green
        
        # Run the application locally
        Write-Host "Launching SharpShot..." -ForegroundColor Yellow
        dotnet run
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "SharpShot started successfully!" -ForegroundColor Green
        } else {
            Write-Host "Failed to start SharpShot. Exit code: $LASTEXITCODE" -ForegroundColor Red
        }
    } else {
        Write-Host "Build failed in Docker!" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "=== Development Workflow ===" -ForegroundColor Cyan
    Write-Host "1. Make changes to your code" -ForegroundColor Gray
    Write-Host "2. Build in Docker: docker exec sharpshot-development dotnet build" -ForegroundColor Gray
    Write-Host "3. Run on host: dotnet run" -ForegroundColor Gray
    Write-Host ""
    Write-Host "=== Quick Commands ===" -ForegroundColor Cyan
    Write-Host "Build in Docker: docker exec sharpshot-development dotnet build" -ForegroundColor Gray
    Write-Host "Run locally: dotnet run" -ForegroundColor Gray
    Write-Host "Stop Docker: docker compose -f docker-compose.dev.yml down" -ForegroundColor Gray
    Write-Host ""
    Write-Host "=== Benefits ===" -ForegroundColor Cyan
    Write-Host "✅ Consistent .NET environment across all PCs" -ForegroundColor Green
    Write-Host "✅ Build in Docker, run on host" -ForegroundColor Green
    Write-Host "✅ No GUI issues in containers" -ForegroundColor Green
    Write-Host "✅ Same build results everywhere" -ForegroundColor Green
} else {
    Write-Host "Failed to start development environment!" -ForegroundColor Red
}

Write-Host "=== Script Complete ===" -ForegroundColor Cyan 