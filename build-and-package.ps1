# SharpShot Build and Package Script
# This script builds SharpShot locally and optionally packages it

Write-Host "=== SharpShot Build and Package ===" -ForegroundColor Cyan

# Navigate to the project directory
Set-Location $PSScriptRoot

# Check if the project file exists
if (Test-Path "SharpShot.csproj") {
    Write-Host "Project found. Building SharpShot..." -ForegroundColor Yellow
    
    # Clean previous builds
    Write-Host "Cleaning previous builds..." -ForegroundColor Gray
    dotnet clean
    
    # Build the application
    Write-Host "Building SharpShot..." -ForegroundColor Yellow
    dotnet build --configuration Release
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build successful!" -ForegroundColor Green
        
        # Publish the application
        Write-Host "Publishing SharpShot..." -ForegroundColor Yellow
        dotnet publish --configuration Release --output ./bin/Release/Publish --runtime win-x64 --self-contained false
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Publish successful!" -ForegroundColor Green
            Write-Host "Application is ready at: ./bin/Release/Publish/SharpShot.exe" -ForegroundColor Cyan
            
            # Ask if user wants to run the application
            $runApp = Read-Host "Do you want to run SharpShot now? (y/n)"
            if ($runApp -eq "y" -or $runApp -eq "Y") {
                Write-Host "Starting SharpShot..." -ForegroundColor Yellow
                & "./bin/Release/Publish/SharpShot.exe"
            }
        } else {
            Write-Host "Publish failed!" -ForegroundColor Red
        }
    } else {
        Write-Host "Build failed!" -ForegroundColor Red
    }
} else {
    Write-Host "Error: SharpShot.csproj not found!" -ForegroundColor Red
}

Write-Host "=== Script Complete ===" -ForegroundColor Cyan 