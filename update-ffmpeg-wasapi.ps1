# Update FFmpeg to WASAPI-enabled build
# This script replaces the current FFmpeg with a full build that includes WASAPI support

Write-Host "Updating FFmpeg to WASAPI-enabled build..." -ForegroundColor Green

# Check if current FFmpeg exists
$currentFfmpegPath = "ffmpeg\bin\ffmpeg.exe"
if (Test-Path $currentFfmpegPath) {
    Write-Host "Current FFmpeg found. Checking if it supports WASAPI..." -ForegroundColor Yellow
    
    # Test current FFmpeg for WASAPI support
    try {
        $process = Start-Process -FilePath $currentFfmpegPath -ArgumentList "-formats" -RedirectStandardOutput -NoNewWindow -PassThru
        $output = $process.StandardOutput.ReadToEnd()
        $process.WaitForExit()
        
        if ($output -match "wasapi") {
            Write-Host "Current FFmpeg already supports WASAPI!" -ForegroundColor Green
            exit 0
        } else {
            Write-Host "Current FFmpeg does not support WASAPI. Updating..." -ForegroundColor Yellow
        }
    } catch {
        Write-Host "Error checking current FFmpeg: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "No current FFmpeg found. Installing new build..." -ForegroundColor Yellow
}

# Backup current FFmpeg if it exists
if (Test-Path "ffmpeg") {
    Write-Host "Backing up current FFmpeg..." -ForegroundColor Yellow
    if (Test-Path "ffmpeg-backup") {
        Remove-Item "ffmpeg-backup" -Recurse -Force
    }
    Rename-Item "ffmpeg" "ffmpeg-backup"
}

# Download and install WASAPI-enabled FFmpeg
Write-Host "Downloading WASAPI-enabled FFmpeg..." -ForegroundColor Yellow

# FFmpeg download URL (Full build with WASAPI support from BtbN/FFmpeg-Builds)
$ffmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
$zipPath = "ffmpeg-wasapi-temp.zip"

try {
    # Download FFmpeg
    Invoke-WebRequest -Uri $ffmpegUrl -OutFile $zipPath -UseBasicParsing
    
    Write-Host "Extracting FFmpeg..." -ForegroundColor Yellow
    
    # Extract the zip file
    Expand-Archive -Path $zipPath -DestinationPath "temp-ffmpeg-wasapi" -Force
    
    # Find the extracted folder
    $extractedFolder = Get-ChildItem -Path "temp-ffmpeg-wasapi" -Directory | Select-Object -First 1
    
    if ($extractedFolder) {
        # Copy the bin folder to our ffmpeg directory
        $binPath = Join-Path $extractedFolder.FullName "bin"
        if (Test-Path $binPath) {
            Copy-Item -Path $binPath -Destination "ffmpeg" -Recurse -Force
            Write-Host "FFmpeg WASAPI build installed!" -ForegroundColor Green
            
            # Test the new FFmpeg
            Write-Host "Testing new FFmpeg..." -ForegroundColor Yellow
            $testProcess = Start-Process -FilePath "ffmpeg\bin\ffmpeg.exe" -ArgumentList "-formats" -RedirectStandardOutput -NoNewWindow -PassThru
            $testOutput = $testProcess.StandardOutput.ReadToEnd()
            $testProcess.WaitForExit()
            
            if ($testOutput -match "wasapi") {
                Write-Host "SUCCESS: New FFmpeg supports WASAPI!" -ForegroundColor Green
                Write-Host "You can now use WASAPI for audio device detection and recording." -ForegroundColor Green
            } else {
                Write-Host "WARNING: New FFmpeg may not support WASAPI. Check the output:" -ForegroundColor Yellow
                Write-Host $testOutput -ForegroundColor Gray
            }
        } else {
            Write-Host "Error: Could not find FFmpeg bin directory" -ForegroundColor Red
        }
    } else {
        Write-Host "Error: Could not find extracted FFmpeg folder" -ForegroundColor Red
    }
    
    # Cleanup
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    Remove-Item "temp-ffmpeg-wasapi" -Recurse -Force -ErrorAction SilentlyContinue
    
} catch {
    Write-Host "Error downloading FFmpeg: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please download FFmpeg manually from https://github.com/BtbN/FFmpeg-Builds" -ForegroundColor Yellow
    Write-Host "and place ffmpeg.exe in the ffmpeg/bin/ directory" -ForegroundColor Yellow
}

Write-Host "FFmpeg update script completed." -ForegroundColor Green 