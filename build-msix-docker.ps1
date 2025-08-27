# Build MSIX using Docker + Local MSIX Creation
# This script uses your working Docker build, then creates MSIX locally

Write-Host "Building SharpShot MSIX package using Docker + Local tools..." -ForegroundColor Green

# Check if Docker is running
try {
    docker version | Out-Null
    Write-Host "Docker is running" -ForegroundColor Green
} catch {
    Write-Host "Docker is not running. Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Step 1: Use your existing working Docker build process
Write-Host "Step 1: Building SharpShot in Docker (using your proven workflow)..." -ForegroundColor Yellow
docker compose -f docker-compose.dev.yml down
docker compose -f docker-compose.dev.yml up --build -d

# Wait for container
Start-Sleep -Seconds 5

# Build in Docker (same as your working Build Release.bat)
Write-Host "Building SharpShot in Docker..." -ForegroundColor Yellow
docker exec sharpshot-development dotnet restore
docker exec sharpshot-development dotnet build --configuration Release -p:Platform=x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker build failed!" -ForegroundColor Red
    docker compose -f docker-compose.dev.yml down
    exit 1
}

Write-Host "Docker build completed successfully!" -ForegroundColor Green

# Step 2: Create MSIX content directory from Docker build output
Write-Host "Step 2: Creating MSIX content from Docker build..." -ForegroundColor Yellow

$outputDir = "bin\Release\msix-content"
$dockerOutputDir = "bin\x64\Release\net8.0-windows\win-x64"

if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# Copy the working build output from Docker
if (Test-Path $dockerOutputDir) {
    Copy-Item "$dockerOutputDir\*" -Destination $outputDir -Recurse -Force
    Write-Host "Copied Docker build output to MSIX content directory" -ForegroundColor Green
} else {
    Write-Host "Error: Docker build output not found at $dockerOutputDir" -ForegroundColor Red
    docker compose -f docker-compose.dev.yml down
    exit 1
}

# Copy manifest and assets
Write-Host "Copying manifest and assets..." -ForegroundColor Yellow
Copy-Item "Package.appxmanifest" -Destination "$outputDir\AppxManifest.xml"
if (Test-Path "Assets") {
    Copy-Item "Assets" -Destination $outputDir -Recurse -Force
}

# Copy OBS Studio and FFmpeg (from your working setup)
Write-Host "Copying OBS Studio and FFmpeg..." -ForegroundColor Yellow
if (Test-Path "OBS-Studio") {
    Copy-Item "OBS-Studio" -Destination $outputDir -Recurse -Force
    Write-Host "OBS Studio copied successfully!" -ForegroundColor Green
}
if (Test-Path "ffmpeg") {
    Copy-Item "ffmpeg" -Destination $outputDir -Recurse -Force
    Write-Host "FFmpeg copied successfully!" -ForegroundColor Green
}

# Step 3: Create MSIX using local Windows SDK tools
Write-Host "Step 3: Creating MSIX package using Windows SDK..." -ForegroundColor Yellow

# Find Windows SDK tools
$sdkPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"
$makeAppxPath = Join-Path $sdkPath "makeappx.exe"

if (-not (Test-Path $makeAppxPath)) {
    # Try to find the latest version
    $sdkPaths = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Directory | Sort-Object Name -Descending
    if ($sdkPaths) {
        $makeAppxPath = Join-Path $sdkPaths[0].FullName "x64\makeappx.exe"
    }
}

if (Test-Path $makeAppxPath) {
    $msixFile = "bin\Release\SharpShot-msix-docker.msix"
    & $makeAppxPath pack /d $outputDir /p $msixFile
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "MSIX package created successfully: $msixFile" -ForegroundColor Green
    } else {
        Write-Host "Error creating MSIX package" -ForegroundColor Red
    }
} else {
    Write-Host "Error: MakeAppx not found. Please install Windows SDK." -ForegroundColor Red
}

# Clean up Docker
Write-Host "Cleaning up Docker environment..." -ForegroundColor Yellow
docker compose -f docker-compose.dev.yml down

Write-Host "MSIX build process completed!" -ForegroundColor Green
Write-Host "Check the bin\Release folder for your .msix file" -ForegroundColor Cyan
