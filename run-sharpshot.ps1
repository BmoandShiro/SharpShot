# SharpShot Build and Run Script
# This script builds SharpShot in Docker and runs it on the host

Write-Host "=== SharpShot Build and Run ===" -ForegroundColor Cyan
Write-Host "Building SharpShot in Docker..." -ForegroundColor Green

# Build the Docker image
docker build -t sharpshot:latest .

if ($LASTEXITCODE -eq 0) {
    Write-Host "Docker build successful!" -ForegroundColor Green
    
    # Create a temporary container to copy the built application
    Write-Host "Extracting built application..." -ForegroundColor Yellow
    docker create --name sharpshot-temp sharpshot:latest
    
    # Create output directory
    $outputDir = ".\bin\Docker"
    if (Test-Path $outputDir) {
        Remove-Item $outputDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    
    # Copy the published application from the container
    docker cp sharpshot-temp:/app/publish/. $outputDir
    
    # Clean up the temporary container
    docker rm sharpshot-temp
    
    Write-Host "Application extracted successfully!" -ForegroundColor Green
    Write-Host "Running SharpShot..." -ForegroundColor Yellow
    
    # Run the application on the host
    & "$outputDir\SharpShot.exe"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "SharpShot started successfully!" -ForegroundColor Green
    } else {
        Write-Host "Failed to start SharpShot. Exit code: $LASTEXITCODE" -ForegroundColor Red
    }
} else {
    Write-Host "Docker build failed! Please check the error messages above." -ForegroundColor Red
}

Write-Host "=== Script Complete ===" -ForegroundColor Cyan 