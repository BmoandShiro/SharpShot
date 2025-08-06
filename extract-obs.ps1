# OBS Studio Extraction Script
# Downloads and extracts OBS Studio to SharpShot/OBS-Studio/

Write-Host "=== OBS Studio Extraction ===" -ForegroundColor Green

$obsUrl = "https://github.com/obsproject/obs-studio/releases/download/31.1.2/OBS-Studio-31.1.2-Full-Installer-x64.exe"
$installerPath = "obs-installer.exe"
$extractPath = "C:\temp\obs-extract"
$targetPath = "OBS-Studio"

try {
    Write-Host "Downloading OBS Studio installer..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $obsUrl -OutFile $installerPath
    
    Write-Host "Extracting OBS Studio..." -ForegroundColor Yellow
    Start-Process -FilePath $installerPath -ArgumentList "/S /D=$extractPath" -Wait -NoNewWindow
    
    Start-Sleep -Seconds 5
    
    if (Test-Path $extractPath) {
        Write-Host "Copying OBS Studio to repo..." -ForegroundColor Yellow
        
        if (Test-Path $targetPath) {
            Remove-Item -Recurse -Force $targetPath
        }
        
        Copy-Item -Path "$extractPath\OBS-Studio" -Destination $targetPath -Recurse -Force
        
        Write-Host "Cleaning up..." -ForegroundColor Yellow
        Remove-Item -Recurse -Force $extractPath
        Remove-Item $installerPath
        
        Write-Host "=== OBS Studio extracted successfully! ===" -ForegroundColor Green
        Write-Host "OBS Studio is now in: $targetPath" -ForegroundColor Cyan
    } else {
        throw "OBS extraction failed"
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} 