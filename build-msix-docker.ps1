# Build MSIX in Docker Environment
# This script uses your existing Docker setup to build the MSIX package

Write-Host "Building SharpShot MSIX package in Docker..." -ForegroundColor Green

# Check if Docker is running
try {
    docker version | Out-Null
    Write-Host "Docker is running" -ForegroundColor Green
} catch {
    Write-Host "Docker is not running. Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Build the MSIX package in Docker
Write-Host "Building MSIX package in Docker container..." -ForegroundColor Yellow

# Create a temporary Dockerfile for MSIX building
$dockerfileContent = @"
FROM mcr.microsoft.com/dotnet/sdk:8.0

# Install Windows SDK tools for MSIX creation
RUN apt-get update && apt-get install -y wget unzip
RUN wget -O windows-sdk.exe "https://go.microsoft.com/fwlink/p/?linkid=2196241"
RUN windows-sdk.exe /quiet /installpath C:\Windows\System32

WORKDIR /app
COPY . ./

# Build the project
RUN dotnet restore
RUN dotnet build --configuration Release -p:Platform=x64

# Generate MSIX package
RUN dotnet publish --configuration Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true

# Copy the MSIX file to a shared volume
RUN cp bin/Release/net8.0-windows/win-x64/publish/*.msix /shared/
"@

$dockerfileContent | Out-File -FilePath "Dockerfile.msix" -Encoding UTF8

# Build and run the container
Write-Host "Creating Docker container for MSIX build..." -ForegroundColor Yellow
docker build -f Dockerfile.msix -t sharpshot-msix-builder .
docker run --rm -v ${PWD}:/shared sharpshot-msix-builder

# Clean up
Remove-Item "Dockerfile.msix" -Force

Write-Host "MSIX build completed in Docker!" -ForegroundColor Green
Write-Host "Check the current directory for your .msix file" -ForegroundColor Cyan
