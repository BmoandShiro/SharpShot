# SharpShot Complete Bundle Build Script
# This script builds SharpShot and bundles both FFmpeg and OBS Studio for distribution
# Uses Docker for reliable building since local builds don't work

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [switch]$SkipDocker,
    [switch]$SkipOBS,
    [switch]$SkipFFmpeg,
    [switch]$CreateZip,
    [switch]$LaunchAfterBuild
)

Write-Host "=== SharpShot Complete Bundle Build ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Platform: $Platform" -ForegroundColor White
Write-Host "Skip Docker: $SkipDocker" -ForegroundColor White
Write-Host "Skip OBS: $SkipOBS" -ForegroundColor White
Write-Host "Skip FFmpeg: $SkipFFmpeg" -ForegroundColor White
Write-Host "Create ZIP: $CreateZip" -ForegroundColor White
Write-Host "Launch After Build: $LaunchAfterBuild" -ForegroundColor White
Write-Host ""

# Set error action preference
$ErrorActionPreference = "Stop"

try {
    # Step 1: Check prerequisites
    Write-Host "Checking prerequisites..." -ForegroundColor Yellow
    
    if (-not $SkipDocker) {
        # Check if Docker is running
        try {
            docker version | Out-Null
            Write-Host "✅ Docker is running" -ForegroundColor Green
        } catch {
            Write-Host "❌ Docker is not running!" -ForegroundColor Red
            Write-Host "Please start Docker Desktop and try again." -ForegroundColor Yellow
            Write-Host "Or use -SkipDocker to build locally (may not work)" -ForegroundColor Yellow
            exit 1
        }
    }
    
    # Check if OBS Studio directory exists
    if (-not $SkipOBS -and -not (Test-Path "OBS-Studio")) {
        Write-Host "❌ OBS-Studio directory not found!" -ForegroundColor Red
        Write-Host "Please run extract-obs.ps1 first to download and extract OBS Studio." -ForegroundColor Yellow
        Write-Host "Or use -SkipOBS to skip OBS bundling" -ForegroundColor Yellow
        exit 1
    }
    
    # Check if FFmpeg directory exists
    if (-not $SkipFFmpeg -and -not (Test-Path "ffmpeg/bin/ffmpeg.exe")) {
        Write-Host "❌ FFmpeg not found in ffmpeg/bin/ffmpeg.exe!" -ForegroundColor Red
        Write-Host "Please ensure FFmpeg is extracted to the ffmpeg directory." -ForegroundColor Yellow
        Write-Host "Or use -SkipFFmpeg to skip FFmpeg bundling" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "✅ All prerequisites met" -ForegroundColor Green
    Write-Host ""
    
    # Step 2: Build the application
    Write-Host "Building SharpShot..." -ForegroundColor Yellow
    
    if ($SkipDocker) {
        # Local build (may not work)
        Write-Host "Building locally (Docker skipped)..." -ForegroundColor Yellow
        dotnet clean --configuration $Configuration
        dotnet restore
        dotnet build --configuration $Configuration -p:Platform=$Platform --no-restore
        
        if ($LASTEXITCODE -ne 0) {
            throw "Local build failed! Try without -SkipDocker to use Docker."
        }
    } else {
        # Docker build (recommended)
        Write-Host "Building in Docker..." -ForegroundColor Yellow
        
        # Stop any existing containers
        Write-Host "Stopping existing containers..." -ForegroundColor Yellow
        docker compose -f docker-compose.dev.yml down
        
        # Build and start the development environment
        Write-Host "Starting Docker environment..." -ForegroundColor Yellow
        docker compose -f docker-compose.dev.yml up --build -d
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to start Docker environment!"
        }
        
        # Wait for container to be ready
        Start-Sleep -Seconds 3
        
        # Build in Docker
        Write-Host "Building in Docker container..." -ForegroundColor Yellow
        docker exec sharpshot-development dotnet clean --configuration $Configuration
        docker exec sharpshot-development dotnet restore --force --no-cache
        
        # Try publish first (for self-contained executable)
        Write-Host "Attempting to publish self-contained executable..." -ForegroundColor Yellow
        docker exec sharpshot-development dotnet publish --configuration $Configuration -p:Platform=$Platform --no-restore --runtime win-x64 --self-contained true --output /app/bin/$Platform/$Configuration/net8.0-windows/win-x64
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Publish failed, trying regular build..." -ForegroundColor Yellow
            docker exec sharpshot-development dotnet build --configuration $Configuration -p:Platform=$Platform --no-restore
        }
        
        if ($LASTEXITCODE -ne 0) {
            throw "Docker build failed!"
        }
        
        # Verify the executable was created
        Write-Host "Verifying build output..." -ForegroundColor Yellow
        $exePath = "bin\$Platform\$Configuration\net8.0-windows\win-x64\SharpShot.exe"
        $dllPath = "bin\$Platform\$Configuration\net8.0-windows\win-x64\SharpShot.dll"
        
        if ((Test-Path $exePath) -or (Test-Path $dllPath)) {
            if (Test-Path $exePath) {
                Write-Host "✅ SharpShot.exe found at: $exePath" -ForegroundColor Green
            } else {
                Write-Host "⚠️  SharpShot.dll found at: $dllPath (not executable)" -ForegroundColor Yellow
                Write-Host "Note: You'll need to run this with 'dotnet SharpShot.dll'" -ForegroundColor Yellow
            }
        } else {
            Write-Host "❌ Neither SharpShot.exe nor SharpShot.dll found!" -ForegroundColor Red
            Write-Host "Checking what files were created..." -ForegroundColor Yellow
            
            # List what was actually created
            $outputPath = "bin\$Platform\$Configuration\net8.0-windows\win-x64"
            if (Test-Path $outputPath) {
                Write-Host "Files in output directory:" -ForegroundColor Yellow
                Get-ChildItem -Path $outputPath -Recurse | ForEach-Object { Write-Host "  $($_.Name)" -ForegroundColor Gray }
            } else {
                Write-Host "Output directory not found: $outputPath" -ForegroundColor Red
            }
            
            throw "Build completed but no SharpShot executable was found. Check the output above."
        }
        
        Write-Host "✅ Build output verified successfully" -ForegroundColor Green
    }
    
    Write-Host "✅ Build completed successfully!" -ForegroundColor Green
    Write-Host ""
    
    # Step 3: Bundle dependencies
    Write-Host "Bundling dependencies..." -ForegroundColor Yellow
    
    $outputDir = "bin\$Configuration\net8.0-windows\win-x64"
    
    # Create output directory if it doesn't exist
    if (!(Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    
    # Bundle OBS Studio
    if (-not $SkipOBS) {
        Write-Host "Bundling OBS Studio..." -ForegroundColor Yellow
        
        $obsSourceDir = "OBS-Studio"
        $obsOutputDir = Join-Path $outputDir "OBS-Studio"
        
        # Remove existing OBS directory
        if (Test-Path $obsOutputDir) {
            Remove-Item $obsOutputDir -Recurse -Force
        }
        
        # Copy OBS Studio with retry logic
        $retryCount = 0
        $maxRetries = 3
        
        while ($retryCount -lt $maxRetries) {
            try {
                Copy-Item -Path $obsSourceDir -Destination $obsOutputDir -Recurse -Force -ErrorAction Stop
                Write-Host "✅ OBS Studio bundled successfully!" -ForegroundColor Green
                break
            } catch {
                $retryCount++
                if ($retryCount -lt $maxRetries) {
                    Write-Host "Copy attempt $retryCount failed. Retrying in 2 seconds..." -ForegroundColor Yellow
                    Start-Sleep -Seconds 2
                } else {
                    Write-Host "❌ Failed to bundle OBS Studio after $maxRetries attempts" -ForegroundColor Red
                    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
                    Write-Host "This might be because OBS Studio is running. Please close OBS Studio and try again." -ForegroundColor Yellow
                    exit 1
                }
            }
        }
    }
    
    # Bundle FFmpeg
    if (-not $SkipFFmpeg) {
        Write-Host "Bundling FFmpeg..." -ForegroundColor Yellow
        
        $ffmpegSourceDir = "ffmpeg"
        $ffmpegOutputDir = Join-Path $outputDir "ffmpeg"
        
        # Remove existing FFmpeg directory
        if (Test-Path $ffmpegOutputDir) {
            Remove-Item $ffmpegOutputDir -Recurse -Force
        }
        
        # Copy FFmpeg
        try {
            Copy-Item -Path $ffmpegSourceDir -Destination $ffmpegOutputDir -Recurse -Force -ErrorAction Stop
            Write-Host "✅ FFmpeg bundled successfully!" -ForegroundColor Green
        } catch {
            Write-Host "❌ Failed to bundle FFmpeg: $($_.Exception.Message)" -ForegroundColor Red
            exit 1
        }
    }
    
    Write-Host "✅ All dependencies bundled successfully!" -ForegroundColor Green
    Write-Host ""
    
    # Step 4: Create distribution package (optional)
    if ($CreateZip) {
        Write-Host "Creating distribution package..." -ForegroundColor Yellow
        
        $distDir = "dist"
        if (Test-Path $distDir) {
            Remove-Item -Recurse -Force $distDir
        }
        
        New-Item -ItemType Directory -Path $distDir | Out-Null
        
        # Copy SharpShot files
        Copy-Item -Path "$outputDir\*" -Destination $distDir -Recurse -Force
        
        # Copy license and documentation
        Copy-Item -Path "LICENSE" -Destination $distDir -Force -ErrorAction SilentlyContinue
        Copy-Item -Path "README.md" -Destination $distDir -Force -ErrorAction SilentlyContinue
        Copy-Item -Path "OBS_INTEGRATION.md" -Destination $distDir -Force -ErrorAction SilentlyContinue
        
        # Create launcher script
        $launcherContent = @"
@echo off
echo Starting SharpShot with bundled dependencies...
echo.
echo Bundled components:
if exist "OBS-Studio" echo - OBS Studio
if exist "ffmpeg" echo - FFmpeg
echo.
start "" "SharpShot.exe"
"@
        
        Set-Content -Path "$distDir\Run SharpShot.bat" -Value $launcherContent
        
        # Create ZIP package
        $zipName = "SharpShot-Complete-$Configuration.zip"
        if (Test-Path $zipName) {
            Remove-Item $zipName
        }
        
        # Use PowerShell to create ZIP
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::CreateFromDirectory($distDir, $zipName)
        
        Write-Host "✅ Distribution package created: $zipName" -ForegroundColor Green
        Write-Host "Size: $((Get-Item $zipName).Length / 1MB) MB" -ForegroundColor Cyan
        Write-Host ""
    }
    
    # Step 5: Summary
    Write-Host "=== Build Summary ===" -ForegroundColor Cyan
    Write-Host "✅ SharpShot built successfully" -ForegroundColor Green
    if (-not $SkipOBS) { Write-Host "✅ OBS Studio bundled" -ForegroundColor Green }
    if (-not $SkipFFmpeg) { Write-Host "✅ FFmpeg bundled" -ForegroundColor Green }
    if ($CreateZip) { Write-Host "✅ Distribution package created" -ForegroundColor Green }
    Write-Host ""
    Write-Host "Output location: $outputDir" -ForegroundColor White
    
    # Step 6: Launch application (optional)
    if ($LaunchAfterBuild) {
        Write-Host ""
        Write-Host "Launching SharpShot..." -ForegroundColor Yellow
        
        if ($SkipDocker) {
            # Local launch
            dotnet run --configuration $Configuration -p:Platform=$Platform
        } else {
            # Launch from built output
            $exePath = Join-Path $outputDir "SharpShot.exe"
            if (Test-Path $exePath) {
                Start-Process $exePath
                Write-Host "✅ SharpShot launched successfully!" -ForegroundColor Green
            } else {
                Write-Host "❌ Could not find SharpShot.exe at: $exePath" -ForegroundColor Red
            }
        }
    }
    
    # Step 7: Cleanup Docker if used
    if (-not $SkipDocker) {
        Write-Host ""
        Write-Host "Docker container is still running for future builds." -ForegroundColor Yellow
        Write-Host "To stop it: docker compose -f docker-compose.dev.yml down" -ForegroundColor Gray
        Write-Host "To view logs: docker logs -f sharpshot-development" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "=== Build Complete! ===" -ForegroundColor Green
    
} catch {
    Write-Host ""
    Write-Host "❌ Build failed: $($_.Exception.Message)" -ForegroundColor Red
    
    if (-not $SkipDocker) {
        Write-Host ""
        Write-Host "Docker troubleshooting:" -ForegroundColor Yellow
        Write-Host "- Check Docker logs: docker logs sharpshot-development" -ForegroundColor Gray
        Write-Host "- Restart Docker: docker compose -f docker-compose.dev.yml down && docker compose -f docker-compose.dev.yml up --build -d" -ForegroundColor Gray
    }
    
    exit 1
}
