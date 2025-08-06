# SharpShot OBS Docker Environment
# This script starts Docker for console output AND builds with OBS bundling

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

Write-Host "=== SharpShot OBS Docker Environment ===" -ForegroundColor Cyan

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

# Stop any existing containers
Write-Host "Stopping any existing containers..." -ForegroundColor Yellow
docker compose -f docker-compose.dev.yml down

# Build and start the development environment
Write-Host "Building Docker development environment..." -ForegroundColor Yellow
docker compose -f docker-compose.dev.yml up --build -d

if ($LASTEXITCODE -eq 0) {
    Write-Host "Docker environment started successfully!" -ForegroundColor Green
    
    # Wait a moment for container to be ready
    Start-Sleep -Seconds 3
    
    # Build in Docker with OBS bundling
    Write-Host "Building SharpShot with OBS bundling in Docker..." -ForegroundColor Yellow
    
    # First, clear any cached NuGet data and do the regular build in Docker
    Write-Host "Clearing NuGet cache in container..." -ForegroundColor Yellow
    docker exec sharpshot-development dotnet nuget locals all --clear
    docker exec sharpshot-development dotnet clean --configuration $Configuration
    docker exec sharpshot-development dotnet restore --force --no-cache
    docker exec sharpshot-development dotnet build --configuration $Configuration -p:Platform=$Platform --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build successful!" -ForegroundColor Green
        
        # Now handle OBS bundling
        Write-Host "Setting up OBS Studio bundling..." -ForegroundColor Yellow
        
        $obsSourceDir = "OBS-Studio"
        $outputDir = "bin\$Configuration\net8.0-windows\$Platform"
        
        # Check if OBS source directory exists
        if (!(Test-Path $obsSourceDir)) {
            Write-Host "OBS-Studio directory not found!" -ForegroundColor Red
            Write-Host "Please ensure the OBS-Studio directory exists with your preferred version." -ForegroundColor Yellow
            Write-Host "Skipping OBS bundling for now..." -ForegroundColor Yellow
        }
        
        # If OBS directory exists, proceed with bundling
        if (Test-Path $obsSourceDir) {
            Write-Host "Bundling OBS Studio..." -ForegroundColor Yellow
            
            # Create output directory if it doesn't exist
            if (!(Test-Path $outputDir)) {
                New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
            }
            
            $obsDestDir = Join-Path $outputDir "OBS-Studio"
            
            # Copy OBS Studio files
            if (Test-Path $obsDestDir) {
                Remove-Item $obsDestDir -Recurse -Force
            }
            Copy-Item $obsSourceDir $obsDestDir -Recurse -Force
            
            Write-Host "OBS Studio bundled successfully!" -ForegroundColor Green
            Write-Host "OBS bundled to: $obsDestDir" -ForegroundColor Gray
        }
        
        Write-Host ""
        Write-Host "=== Docker Environment Ready ===" -ForegroundColor Cyan
        Write-Host "✅ Docker container is running" -ForegroundColor Green
        Write-Host "✅ SharpShot built successfully" -ForegroundColor Green
        Write-Host "✅ OBS Studio bundled (if available)" -ForegroundColor Green
        Write-Host ""
        Write-Host "=== View Container Logs ===" -ForegroundColor Cyan
        Write-Host "To see live container output:" -ForegroundColor Yellow
        Write-Host "  docker logs -f sharpshot-development" -ForegroundColor Gray
        Write-Host ""
        Write-Host "=== Run Application ===" -ForegroundColor Cyan
        Write-Host "Run locally: dotnet run" -ForegroundColor Gray
        Write-Host "Build in Docker: docker exec sharpshot-development dotnet build -p:Platform=x64" -ForegroundColor Gray
        Write-Host ""
        Write-Host "=== Management Commands ===" -ForegroundColor Cyan
        Write-Host "Stop container: docker compose -f docker-compose.dev.yml down" -ForegroundColor Gray
        Write-Host "View logs: docker logs -f sharpshot-development" -ForegroundColor Gray
        Write-Host ""
        
        # Automatically launch SharpShot
        Write-Host "Launching SharpShot..." -ForegroundColor Yellow
        dotnet run
        
    } else {
        Write-Host "Build failed in Docker!" -ForegroundColor Red
        Write-Host "Check the container logs: docker logs sharpshot-development" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "Failed to start Docker environment!" -ForegroundColor Red
    exit 1
}

Write-Host "=== OBS Docker Environment Complete ===" -ForegroundColor Cyan