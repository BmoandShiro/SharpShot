# SharpShot Live Development Management Script
# This script provides various commands for managing the live development environment

param(
    [Parameter(Position=0)]
    [ValidateSet("start", "stop", "restart", "logs", "build", "run", "status", "clean")]
    [string]$Command = "status"
)

Write-Host "=== SharpShot Live Development Manager ===" -ForegroundColor Cyan

# Navigate to project directory
Set-Location $PSScriptRoot

function Start-LiveDev {
    Write-Host "Starting live development environment..." -ForegroundColor Yellow
    docker compose -f docker-compose.live-dev.yml up --build -d
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Live development environment started!" -ForegroundColor Green
        Write-Host "Container is now watching for code changes..." -ForegroundColor Yellow
    } else {
        Write-Host "Failed to start live development environment!" -ForegroundColor Red
    }
}

function Stop-LiveDev {
    Write-Host "Stopping live development environment..." -ForegroundColor Yellow
    docker compose -f docker-compose.live-dev.yml down
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Live development environment stopped!" -ForegroundColor Green
    } else {
        Write-Host "Failed to stop live development environment!" -ForegroundColor Red
    }
}

function Restart-LiveDev {
    Write-Host "Restarting live development environment..." -ForegroundColor Yellow
    Stop-LiveDev
    Start-Sleep -Seconds 2
    Start-LiveDev
}

function Show-Logs {
    Write-Host "Showing live development logs..." -ForegroundColor Yellow
    docker logs -f sharpshot-live-development
}

function Build-Project {
    Write-Host "Building project in Docker..." -ForegroundColor Yellow
    docker exec sharpshot-live-development dotnet build
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build successful!" -ForegroundColor Green
    } else {
        Write-Host "Build failed!" -ForegroundColor Red
    }
}

function Run-Application {
    Write-Host "Running SharpShot application..." -ForegroundColor Yellow
    dotnet run
}

function Show-Status {
    Write-Host "Checking live development environment status..." -ForegroundColor Yellow
    
    $containerStatus = docker ps --filter "name=sharpshot-live-development" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
    
    if ($containerStatus -match "sharpshot-live-development") {
        Write-Host "✅ Live development container is running" -ForegroundColor Green
        Write-Host $containerStatus
    } else {
        Write-Host "❌ Live development container is not running" -ForegroundColor Red
        Write-Host "Use 'start' command to start the environment" -ForegroundColor Yellow
    }
}

function Clean-Environment {
    Write-Host "Cleaning live development environment..." -ForegroundColor Yellow
    docker compose -f docker-compose.live-dev.yml down -v
    docker system prune -f
    Write-Host "Environment cleaned!" -ForegroundColor Green
}

# Execute the requested command
switch ($Command) {
    "start" { Start-LiveDev }
    "stop" { Stop-LiveDev }
    "restart" { Restart-LiveDev }
    "logs" { Show-Logs }
    "build" { Build-Project }
    "run" { Run-Application }
    "status" { Show-Status }
    "clean" { Clean-Environment }
    default { 
        Write-Host "Unknown command: $Command" -ForegroundColor Red
        Write-Host "Available commands: start, stop, restart, logs, build, run, status, clean" -ForegroundColor Yellow
    }
}

Write-Host "=== Command Complete ===" -ForegroundColor Cyan 