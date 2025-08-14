# Simple MSIX Build Script for SharpShot
# This script uses dotnet publish with built-in MSIX generation

param(
    [string]$Configuration = "Release"
)

Write-Host "Building SharpShot MSIX package..." -ForegroundColor Green

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean SharpShot.csproj --configuration $Configuration

# Restore packages
Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore SharpShot.csproj

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build SharpShot.csproj --configuration $Configuration --no-restore

# Publish with MSIX generation
Write-Host "Generating MSIX package..." -ForegroundColor Yellow
dotnet publish SharpShot.csproj --configuration $Configuration --no-build -p:GenerateAppxPackageOnBuild=true

Write-Host "MSIX build completed!" -ForegroundColor Green
Write-Host "Check the bin\Release\net8.0-windows\win-x64\publish folder for your .msix file" -ForegroundColor Cyan
