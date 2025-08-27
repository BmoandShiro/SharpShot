# Build SharpShot MSIX using Windows SDK
# This script uses the Windows SDK tools installed on your system

param(
    [string]$Configuration = "Release"
)

Write-Host "Building SharpShot MSIX using Windows SDK..." -ForegroundColor Green

# Windows SDK paths
$sdkPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"
$makeAppxPath = Join-Path $sdkPath "makeappx.exe"
$signToolPath = Join-Path $sdkPath "signtool.exe"

# Verify SDK tools exist
if (-not (Test-Path $makeAppxPath)) {
    Write-Host "Error: MakeAppx not found at $makeAppxPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $signToolPath)) {
    Write-Host "Error: SignTool not found at $signToolPath" -ForegroundColor Red
    exit 1
}

Write-Host "Windows SDK tools found:" -ForegroundColor Green
Write-Host "  MakeAppx: $makeAppxPath" -ForegroundColor Cyan
Write-Host "  SignTool: $signToolPath" -ForegroundColor Cyan

# Clean and build
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean SharpShot.csproj --configuration $Configuration

Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore SharpShot.csproj

Write-Host "Building project..." -ForegroundColor Yellow
dotnet build SharpShot.csproj --configuration $Configuration --no-restore

# Create output directories
$outputDir = "bin\$Configuration\msix-content"
$publishDir = "bin\$Configuration\net8.0-windows\win-x64\publish"

if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# Copy published files
Write-Host "Copying published files..." -ForegroundColor Yellow
if (Test-Path $publishDir) {
    Copy-Item "$publishDir\*" -Destination $outputDir -Recurse
} else {
    Write-Host "Error: Publish directory not found. Building with publish..." -ForegroundColor Yellow
    dotnet publish SharpShot.csproj --configuration $Configuration --no-build
    if (Test-Path $publishDir) {
        Copy-Item "$publishDir\*" -Destination $outputDir -Recurse
    } else {
        Write-Host "Error: Publish failed" -ForegroundColor Red
        exit 1
    }
}

# Copy manifest and assets
Write-Host "Copying manifest and assets..." -ForegroundColor Yellow
Copy-Item "Package.appxmanifest" -Destination $outputDir
if (Test-Path "Assets") {
    Copy-Item "Assets" -Destination $outputDir -Recurse
}

# Create MSIX package
Write-Host "Creating MSIX package..." -ForegroundColor Yellow
$msixFile = "bin\$Configuration\SharpShot.msix"
& $makeAppxPath pack /d $outputDir /p $msixFile

if ($LASTEXITCODE -eq 0) {
    Write-Host "MSIX package created successfully: $msixFile" -ForegroundColor Green
} else {
    Write-Host "Error creating MSIX package" -ForegroundColor Red
    exit 1
}

Write-Host "MSIX build completed!" -ForegroundColor Green
Write-Host "File: $msixFile" -ForegroundColor Cyan
