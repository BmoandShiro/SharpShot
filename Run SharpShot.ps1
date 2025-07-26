# SharpShot Quick Run Script
# This script runs SharpShot directly

Write-Host "=== SharpShot Quick Run ===" -ForegroundColor Cyan
Write-Host "Starting SharpShot..." -ForegroundColor Green

# Navigate to the project directory
Set-Location $PSScriptRoot

# Check if the project file exists
if (Test-Path "SharpShot.csproj") {
    Write-Host "Project found. Building and running SharpShot..." -ForegroundColor Yellow
    
    # Check if dotnet is available
    try {
        $dotnetVersion = dotnet --version
        Write-Host "Using .NET version: $dotnetVersion" -ForegroundColor Gray
    } catch {
        Write-Host "Error: .NET SDK not found!" -ForegroundColor Red
        Write-Host "Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download" -ForegroundColor Yellow
        exit 1
    }
    
    # Build and run the application
    Write-Host "Building and running SharpShot..." -ForegroundColor Yellow
    dotnet run
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "SharpShot started successfully!" -ForegroundColor Green
    } else {
        Write-Host "Failed to start SharpShot. Exit code: $LASTEXITCODE" -ForegroundColor Red
    }
} else {
    Write-Host "Error: SharpShot.csproj not found!" -ForegroundColor Red
    Write-Host "Please run this script from the SharpShot project directory." -ForegroundColor Yellow
}

Write-Host "=== Script Complete ===" -ForegroundColor Cyan 