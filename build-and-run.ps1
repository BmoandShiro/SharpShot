# SharpShot Docker Build and Run Script
# This script builds and runs SharpShot in a Docker container

Write-Host "Building SharpShot Docker container..." -ForegroundColor Green

# Build the Docker image
docker build -t sharpshot:latest .

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful! Running SharpShot..." -ForegroundColor Green
    
    # Run the container
    docker run -it --rm `
        --name sharpshot-app `
        -v "${env:USERPROFILE}\Pictures:C:\app\Pictures" `
        -v "${env:APPDATA}\SharpShot:C:\app\Settings" `
        sharpshot:latest
} else {
    Write-Host "Build failed! Please check the error messages above." -ForegroundColor Red
} 