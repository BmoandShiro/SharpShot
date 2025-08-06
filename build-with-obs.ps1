# SharpShot Build Script with OBS Bundling
# This script builds SharpShot and bundles OBS Studio for distribution

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

Write-Host "=== SharpShot Build with OBS Bundling ===" -ForegroundColor Green

# Set error action preference
$ErrorActionPreference = "Stop"

try {
    # Step 1: Clean previous builds
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean --configuration $Configuration
    
    # Step 2: Restore dependencies
    Write-Host "Restoring dependencies..." -ForegroundColor Yellow
    dotnet restore
    
    # Step 3: Build the application
    Write-Host "Building SharpShot..." -ForegroundColor Yellow
    dotnet build --configuration $Configuration -p:Platform=$Platform --no-restore
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed!"
    }
    
    Write-Host "Build completed successfully!" -ForegroundColor Green
    
    # Step 4: Bundle local OBS Studio
    Write-Host "Setting up OBS Studio bundling..." -ForegroundColor Yellow
    
    $obsSourceDir = "OBS-Studio"
    $outputDir = "bin\$Configuration\net8.0-windows\$Platform"
    
    # Create output directory if it doesn't exist
    if (!(Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    
    # Check if OBS source directory exists
    if (!(Test-Path $obsSourceDir)) {
        Write-Host "OBS-Studio directory not found!" -ForegroundColor Red
        Write-Host "Please run extract-obs.ps1 first to download and extract OBS Studio." -ForegroundColor Yellow
        Write-Host "Or manually extract OBS Studio to the 'OBS-Studio' directory." -ForegroundColor Yellow
        exit 1
    }
    
    # Copy bundled OBS to the output directory
    Write-Host "Copying bundled OBS Studio to output directory..." -ForegroundColor Yellow
    $obsOutputDir = Join-Path $outputDir "OBS-Studio"
    if (Test-Path $obsOutputDir) {
        try {
            Remove-Item -Recurse -Force $obsOutputDir -ErrorAction Stop
        } catch {
            Write-Host "Warning: Could not remove existing OBS directory. Trying to copy anyway..." -ForegroundColor Yellow
        }
    }
    
    try {
        # Try to copy with retry logic
        $retryCount = 0
        $maxRetries = 3
        
        while ($retryCount -lt $maxRetries) {
            try {
                Copy-Item -Path $obsSourceDir -Destination $obsOutputDir -Recurse -Force -ErrorAction Stop
                Write-Host "OBS Studio copied successfully!" -ForegroundColor Green
                break
            } catch {
                $retryCount++
                if ($retryCount -lt $maxRetries) {
                    Write-Host "Copy attempt $retryCount failed. Retrying in 2 seconds..." -ForegroundColor Yellow
                    Start-Sleep -Seconds 2
                } else {
                    Write-Host "Error copying OBS Studio: $($_.Exception.Message)" -ForegroundColor Red
                    Write-Host "This might be because OBS Studio is running. Please close OBS Studio and try again." -ForegroundColor Yellow
                    Write-Host "Or try running the build script as Administrator." -ForegroundColor Yellow
                    exit 1
                }
            }
        }
    } catch {
        Write-Host "Error copying OBS Studio: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "This might be because OBS Studio is running. Please close OBS Studio and try again." -ForegroundColor Yellow
        exit 1
    }
    
    # Step 5: Create distribution package
    Write-Host "Creating distribution package..." -ForegroundColor Yellow
    
    $distDir = "dist"
    if (Test-Path $distDir) {
        Remove-Item -Recurse -Force $distDir
    }
    
    New-Item -ItemType Directory -Path $distDir | Out-Null
    
    # Copy SharpShot files
    Copy-Item -Path $outputDir\* -Destination $distDir -Recurse -Force
    
    # Copy OBS Studio
    if (Test-Path $obsOutputDir) {
        Copy-Item -Path $obsOutputDir -Destination $distDir -Recurse -Force
    }
    
    # Copy license and documentation
    Copy-Item -Path "LICENSE" -Destination $distDir -Force
    Copy-Item -Path "README.md" -Destination $distDir -Force
    Copy-Item -Path "OBS_INTEGRATION.md" -Destination $distDir -Force
    
    # Create a simple launcher script
    $launcherContent = @"
@echo off
echo Starting SharpShot with bundled OBS Studio...
start "" "SharpShot.exe"
"@
    
    Set-Content -Path "$distDir\Run SharpShot.bat" -Value $launcherContent
    
    # Step 6: Create ZIP package
    Write-Host "Creating ZIP package..." -ForegroundColor Yellow
    
    $zipName = "SharpShot-with-OBS-$Configuration.zip"
    if (Test-Path $zipName) {
        Remove-Item $zipName
    }
    
    # Use PowerShell to create ZIP (requires .NET Framework 4.5+)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($distDir, $zipName)
    
    # Step 7: Cleanup
    Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
    # No cleanup needed - using local OBS directory
    
    Write-Host "=== Build Complete! ===" -ForegroundColor Green
    Write-Host "Distribution package created: $zipName" -ForegroundColor Cyan
    Write-Host "Size: $((Get-Item $zipName).Length / 1MB) MB" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "The package includes:" -ForegroundColor White
    Write-Host "- SharpShot application" -ForegroundColor White
    Write-Host "- Bundled OBS Studio" -ForegroundColor White
    Write-Host "- Documentation and licenses" -ForegroundColor White
    Write-Host "- Launcher script" -ForegroundColor White
    
    # Step 8: Launch the application
    Write-Host ""
    Write-Host "Launching SharpShot..." -ForegroundColor Yellow
    try {
        dotnet run --configuration $Configuration -p:Platform=$Platform
        Write-Host "SharpShot launched successfully!" -ForegroundColor Green
    } catch {
        Write-Host "Failed to launch SharpShot: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "You can manually run: dotnet run --configuration $Configuration -p:Platform=$Platform" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "Error during build: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} 