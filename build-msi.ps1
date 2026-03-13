param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

Write-Host "Building SharpShot MSI package..." -ForegroundColor Green

$portableDir = "SharpShot-Release-v1.2.8.3"
$installerDir = "Installer"
$productWxs = Join-Path $installerDir "SharpShot.wxs"
$harvestWxs = Join-Path $installerDir "SharpShot.Harvest.wxs"

if (-not (Test-Path $portableDir)) {
    Write-Host "[ERROR] Portable release folder '$portableDir' not found." -ForegroundColor Red
    Write-Host "Run 'Build Release.bat' (or 3xbuild.bat step 1) first, then try again." -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $productWxs)) {
    Write-Host "[ERROR] WiX product file '$productWxs' not found." -ForegroundColor Red
    Write-Host "Expected WiX definition for SharpShot MSI at that location." -ForegroundColor Yellow
    exit 1
}

function Get-ToolPath {
    param(
        [string]$ToolName
    )
    $cmd = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Path }
    return $null
}

function Get-WixBinDir {
    $searchRoots = @(
        [Environment]::GetFolderPath("ProgramFilesX86"),
        [Environment]::GetFolderPath("ProgramFiles")
    )
    foreach ($root in $searchRoots) {
        if (-not (Test-Path $root)) { continue }
        $wixDirs = Get-ChildItem -Path $root -Directory -Filter "WiX Toolset*" -ErrorAction SilentlyContinue
        foreach ($wix in $wixDirs) {
            $binDir = Join-Path $wix.FullName "bin"
            if (Test-Path (Join-Path $binDir "heat.exe")) { return $binDir }
        }
    }
    $fixedVersions = @("v3.11", "v3.10", "v3.9", "3.11", "3.10")
    foreach ($root in @("C:\Program Files (x86)", "C:\Program Files")) {
        if (-not (Test-Path $root)) { continue }
        foreach ($ver in $fixedVersions) {
            $dir = Join-Path $root "WiX Toolset $ver\bin"
            if (Test-Path (Join-Path $dir "heat.exe")) { return $dir }
        }
    }
    return $null
}

$heatPath = Get-ToolPath "heat.exe"
$candlePath = Get-ToolPath "candle.exe"
$lightPath = Get-ToolPath "light.exe"

if (-not $heatPath -or -not $candlePath -or -not $lightPath) {
    $wixBin = Get-WixBinDir
    if ($wixBin) {
        $heatPath = Join-Path $wixBin "heat.exe"
        $candlePath = Join-Path $wixBin "candle.exe"
        $lightPath = Join-Path $wixBin "light.exe"
        Write-Host "Using WiX from: $wixBin" -ForegroundColor Cyan
    }
}

if (-not $heatPath -or -not $candlePath -or -not $lightPath) {
    Write-Host "[ERROR] WiX Toolset (heat.exe, candle.exe, light.exe) not found." -ForegroundColor Red
    Write-Host "Install WiX Toolset v3 from: https://wixtoolset.org/docs/wix3/" -ForegroundColor Yellow
    Write-Host "Or add its bin folder to PATH (e.g. 'C:\Program Files (x86)\WiX Toolset v3.11\bin')." -ForegroundColor Yellow
    exit 1
}

Write-Host "Using WiX tools from:" -ForegroundColor Cyan
Write-Host "  heat.exe   : $heatPath" -ForegroundColor Cyan
Write-Host "  candle.exe : $candlePath" -ForegroundColor Cyan
Write-Host "  light.exe  : $lightPath" -ForegroundColor Cyan

if (-not (Test-Path $installerDir)) {
    New-Item -ItemType Directory -Path $installerDir | Out-Null
}

Write-Host "Step 1: Harvesting files from '$portableDir'..." -ForegroundColor Yellow
# Use a WiX preprocessor variable for the source path so light.exe can resolve it via -dSourceDir
& $heatPath dir $portableDir -cg SharpShotComponents -gg -g1 -dr INSTALLDIR -srd -sreg -var var.SourceDir -out $harvestWxs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $harvestWxs)) {
    Write-Host "[ERROR] heat.exe failed to generate '$harvestWxs'." -ForegroundColor Red
    exit 1
}

Write-Host "Step 2: Compiling WiX sources..." -ForegroundColor Yellow
$productObj = Join-Path $installerDir "SharpShot.wixobj"
$harvestObj = Join-Path $installerDir "SharpShot.Harvest.wixobj"

& $candlePath -dSourceDir="$portableDir" -arch $Platform -out $productObj $productWxs
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] candle.exe failed on '$productWxs'." -ForegroundColor Red
    exit 1
}

& $candlePath -dSourceDir="$portableDir" -arch $Platform -out $harvestObj $harvestWxs
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] candle.exe failed on '$harvestWxs'." -ForegroundColor Red
    exit 1
}

$outputDir = "bin\$Configuration"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$msiName = "SharpShot-1.2.8.3.msi"
$msiPath = Join-Path $outputDir $msiName

Write-Host "Step 3: Linking MSI '$msiPath'..." -ForegroundColor Yellow
# Pass SourceDir so harvested file paths resolve correctly
& $lightPath -dSourceDir="$portableDir" -out $msiPath $productObj $harvestObj -ext WixUIExtension
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $msiPath)) {
    Write-Host "[ERROR] light.exe failed to create MSI." -ForegroundColor Red
    exit 1
}

Write-Host "" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "    MSI BUILD COMPLETE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "MSI created at:" -ForegroundColor White
Write-Host "  $msiPath" -ForegroundColor Cyan

