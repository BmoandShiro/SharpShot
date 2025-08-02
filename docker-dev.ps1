# SharpShot Docker Development Script
# This script provides a consistent development environment across different Windows PCs

param(
    [string]$Command = "help"
)

Write-Host "SharpShot Docker Development Environment" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green

switch ($Command.ToLower()) {
    "build" {
        Write-Host "Building SharpShot in Docker..." -ForegroundColor Yellow
        docker-compose -f docker-compose.dev.yml build
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Build completed successfully!" -ForegroundColor Green
        } else {
            Write-Host "Build failed!" -ForegroundColor Red
        }
    }
    
    "start" {
        Write-Host "Starting SharpShot development environment..." -ForegroundColor Yellow
        docker-compose -f docker-compose.dev.yml up -d
        Write-Host "Development environment started!" -ForegroundColor Green
        Write-Host "To access the container, run: docker exec -it sharpshot-development cmd" -ForegroundColor Cyan
    }
    
    "stop" {
        Write-Host "Stopping SharpShot development environment..." -ForegroundColor Yellow
        docker-compose -f docker-compose.dev.yml down
        Write-Host "Development environment stopped!" -ForegroundColor Green
    }
    
    "shell" {
        Write-Host "Opening shell in SharpShot development container..." -ForegroundColor Yellow
        docker exec -it sharpshot-development cmd
    }
    
    "build-app" {
        Write-Host "Building SharpShot application..." -ForegroundColor Yellow
        docker exec -it sharpshot-development dotnet build --configuration Release -p:Platform=x64
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Application built successfully!" -ForegroundColor Green
            Write-Host "Output available in ./output directory" -ForegroundColor Cyan
        } else {
            Write-Host "Build failed!" -ForegroundColor Red
        }
    }
    
    "clean" {
        Write-Host "Cleaning Docker environment..." -ForegroundColor Yellow
        docker-compose -f docker-compose.dev.yml down -v
        docker system prune -f
        Write-Host "Cleanup completed!" -ForegroundColor Green
    }
    
    "help" {
        Write-Host "Available commands:" -ForegroundColor Cyan
        Write-Host "  build     - Build the Docker development image" -ForegroundColor White
        Write-Host "  start     - Start the development environment" -ForegroundColor White
        Write-Host "  stop      - Stop the development environment" -ForegroundColor White
        Write-Host "  shell     - Open shell in development container" -ForegroundColor White
        Write-Host "  build-app - Build the SharpShot application" -ForegroundColor White
        Write-Host "  clean     - Clean up Docker resources" -ForegroundColor White
        Write-Host "  help      - Show this help message" -ForegroundColor White
        Write-Host ""
        Write-Host "Usage: .\docker-dev.ps1 <command>" -ForegroundColor Yellow
    }
    
    default {
        Write-Host "Unknown command: $Command" -ForegroundColor Red
        Write-Host "Run '.\docker-dev.ps1 help' for available commands" -ForegroundColor Yellow
    }
} 