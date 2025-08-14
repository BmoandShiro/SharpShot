# SharpShot MSIX Build Script for Microsoft Store
# This script builds the MSIX bundle required for Microsoft Store submission

param(
    [string]$Configuration = "Release",
    [switch]$Clean
)

Write-Host "Building SharpShot MSIX Bundle for Microsoft Store..." -ForegroundColor Green

# Set error action preference
$ErrorActionPreference = "Stop"

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean --configuration $Configuration
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
}

# Restore packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build --configuration $Configuration --no-restore

# Publish with MSIX generation enabled
Write-Host "Publishing MSIX package..." -ForegroundColor Yellow
dotnet publish --configuration $Configuration --no-build -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false

Write-Host "Build completed!" -ForegroundColor Green
Write-Host "Check the bin\$Configuration folder for output files." -ForegroundColor Cyan
Write-Host "Look for .msix and .msixbundle files in the publish directory." -ForegroundColor Cyan
