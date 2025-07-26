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