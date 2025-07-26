# SharpShot Distribution Package Creator
# This script creates a portable package that can run on any Windows PC

Write-Host "=== SharpShot Distribution Package ===" -ForegroundColor Cyan

# Navigate to the project directory
Set-Location $PSScriptRoot

# Check if the published application exists
$publishPath = ".\bin\Release\net8.0-windows\win-x64"
if (Test-Path "$publishPath\SharpShot.exe") {
    Write-Host "Found published application!" -ForegroundColor Green
    
    # Create distribution directory
    $distDir = ".\SharpShot-Portable"
    if (Test-Path $distDir) {
        Remove-Item $distDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
    
    # Copy all files to distribution directory
    Write-Host "Creating portable package..." -ForegroundColor Yellow
    Copy-Item "$publishPath\*" $distDir -Recurse -Force
    
    # Create a simple README
    $readmeContent = @"
SharpShot - Portable Screenshot Tool

This is a portable version of SharpShot that can run on any Windows PC.

To run:
1. Double-click SharpShot.exe
2. The floating toolbar will appear in the top-right corner
3. Use the buttons to capture screenshots

Features:
- Region selection (🔲)
- Full screen capture (📸)
- Copy to clipboard (Copy)
- Save to file (💾)

No installation required!
"@
    
    Set-Content -Path "$distDir\README.txt" -Value $readmeContent
    
    # Create a simple launcher batch file
    $launcherContent = @"
@echo off
echo Starting SharpShot...
start SharpShot.exe
"@
    
    Set-Content -Path "$distDir\Run SharpShot.bat" -Value $launcherContent
    
    Write-Host "Portable package created successfully!" -ForegroundColor Green
    Write-Host "Location: $distDir" -ForegroundColor Cyan
    Write-Host "Size: $((Get-ChildItem $distDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB) MB" -ForegroundColor Gray
    
    # Ask if user wants to open the folder
    $openFolder = Read-Host "Do you want to open the distribution folder? (y/n)"
    if ($openFolder -eq "y" -or $openFolder -eq "Y") {
        explorer $distDir
    }
    
} else {
    Write-Host "Error: Published application not found!" -ForegroundColor Red
    Write-Host "Please run build-and-package.ps1 first." -ForegroundColor Yellow
}

Write-Host "=== Script Complete ===" -ForegroundColor Cyan 