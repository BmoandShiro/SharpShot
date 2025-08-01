# Setup FFmpeg for SharpShot
# This script downloads and extracts FFmpeg for the application

param(
    [string]$OutputPath = "ffmpeg"
)

Write-Host "Setting up FFmpeg for SharpShot..." -ForegroundColor Green

# Create output directory
if (!(Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath | Out-Null
}

# FFmpeg download URL (Full build with WASAPI support from BtbN/FFmpeg-Builds)
$ffmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
$zipPath = "ffmpeg-temp.zip"

Write-Host "Downloading FFmpeg..." -ForegroundColor Yellow
try {
    # Download FFmpeg
    Invoke-WebRequest -Uri $ffmpegUrl -OutFile $zipPath -UseBasicParsing
    
    Write-Host "Extracting FFmpeg..." -ForegroundColor Yellow
    
    # Extract the zip file
    Expand-Archive -Path $zipPath -DestinationPath "temp-ffmpeg" -Force
    
    # Find the extracted folder (BtbN builds have a different structure)
    $extractedFolder = Get-ChildItem -Path "temp-ffmpeg" -Directory | Select-Object -First 1
    
    if ($extractedFolder) {
        # Copy the bin folder to our ffmpeg directory
        $binPath = Join-Path $extractedFolder.FullName "bin"
        if (Test-Path $binPath) {
            Copy-Item -Path $binPath -Destination $OutputPath -Recurse -Force
            Write-Host "FFmpeg setup complete!" -ForegroundColor Green
        } else {
            # Try alternative structure (some builds have bin directly in root)
            $altBinPath = Join-Path $extractedFolder.FullName "bin"
            if (Test-Path $altBinPath) {
                Copy-Item -Path $altBinPath -Destination $OutputPath -Recurse -Force
                Write-Host "FFmpeg setup complete!" -ForegroundColor Green
            } else {
                Write-Host "Error: Could not find FFmpeg bin directory" -ForegroundColor Red
            }
        }
    } else {
        Write-Host "Error: Could not find extracted FFmpeg folder" -ForegroundColor Red
    }
    
    # Cleanup
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    Remove-Item "temp-ffmpeg" -Recurse -Force -ErrorAction SilentlyContinue
    
} catch {
    Write-Host "Error downloading FFmpeg: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please download FFmpeg manually from https://ffmpeg.org/download.html" -ForegroundColor Yellow
    Write-Host "and place ffmpeg.exe in the ffmpeg/bin/ directory" -ForegroundColor Yellow
}

Write-Host "FFmpeg setup script completed." -ForegroundColor Green 