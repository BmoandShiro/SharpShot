# Apply-Version.ps1
# Reads the Version file and updates all project files so the version number is consistent.
# Run from the SharpShot project directory (or pass -ProjectDir). Called automatically by build-release.ps1.

param(
    [string]$ProjectDir = $PSScriptRoot
)

Set-Location $ProjectDir

$versionFile = Join-Path $ProjectDir "Version"
if (-not (Test-Path $versionFile)) {
    Write-Host "Version file not found: $versionFile" -ForegroundColor Red
    exit 1
}

$version = (Get-Content $versionFile -Raw).Trim()
# Validate format: at least major.minor (e.g. 1.2 or 1.2.8.0)
if ($version -notmatch '^\d+\.\d+(\.\d+(\.\d+)?)?$') {
    Write-Host "Invalid version format in Version file: '$version'. Use e.g. 1.2.8.0" -ForegroundColor Red
    exit 1
}

# Ensure we have 4 parts for assembly/installer (pad with .0 if needed)
$parts = $version.Split('.')
if ($parts.Length -eq 2) { $version = "$version.0.0" }
elseif ($parts.Length -eq 3) { $version = "$version.0" }

$releaseFolder = "SharpShot-Release-v$version"
$msiName = "SharpShot-$version.msi"

Write-Host "Applying version $version to project files..." -ForegroundColor Cyan

function Update-FileContent {
    param([string]$Path, [string]$Content)
    if (Test-Path $Path) {
        [System.IO.File]::WriteAllText((Resolve-Path $Path).Path, $Content)
    }
}

# Match any existing 4-part version
$versionPattern = '\d+\.\d+\.\d+\.\d+'
$releaseFolderPattern = 'SharpShot-Release-v\d+\.\d+\.\d+\.\d+'
$msiNamePattern = 'SharpShot-\d+\.\d+\.\d+\.\d+\.msi'

# 1. SharpShot.csproj
$csprojPath = Join-Path $ProjectDir "SharpShot.csproj"
if (Test-Path $csprojPath) {
    $content = Get-Content $csprojPath -Raw
    $content = $content -replace "<AssemblyVersion>$versionPattern</AssemblyVersion>", "<AssemblyVersion>$version</AssemblyVersion>"
    $content = $content -replace "<FileVersion>$versionPattern</FileVersion>", "<FileVersion>$version</FileVersion>"
    Update-FileContent $csprojPath $content
    Write-Host "  Updated SharpShot.csproj" -ForegroundColor Gray
}

# 2. UI\SettingsWindow.xaml (VersionTextBlock Text="v...")
$xamlPath = Join-Path $ProjectDir "UI\SettingsWindow.xaml"
if (Test-Path $xamlPath) {
    $content = Get-Content $xamlPath -Raw
    $content = $content -replace 'Text="v\d+\.\d+\.\d+\.\d+"', "Text=`"v$version`""
    Update-FileContent $xamlPath $content
    Write-Host "  Updated UI\SettingsWindow.xaml" -ForegroundColor Gray
}

# 3. Installer\SharpShot.wxs
$wxsPath = Join-Path $ProjectDir "Installer\SharpShot.wxs"
if (Test-Path $wxsPath) {
    $content = Get-Content $wxsPath -Raw
    $content = $content -replace "Version=`"$versionPattern`"", "Version=`"$version`""
    Update-FileContent $wxsPath $content
    Write-Host "  Updated Installer\SharpShot.wxs" -ForegroundColor Gray
}

# 4. Installer\SharpShot.nsi
$nsiPath = Join-Path $ProjectDir "Installer\SharpShot.nsi"
if (Test-Path $nsiPath) {
    $content = Get-Content $nsiPath -Raw
    $content = $content -replace '!define APP_VERSION "\d+\.\d+\.\d+\.\d+"', "!define APP_VERSION `"$version`""
    $content = $content -replace '!define APP_PORTABLE_DIR "SharpShot-Release-v\d+\.\d+\.\d+\.\d+"', "!define APP_PORTABLE_DIR `"$releaseFolder`""
    Update-FileContent $nsiPath $content
    Write-Host "  Updated Installer\SharpShot.nsi" -ForegroundColor Gray
}

# 5. build-msi.ps1
$msiScriptPath = Join-Path $ProjectDir "build-msi.ps1"
if (Test-Path $msiScriptPath) {
    $content = Get-Content $msiScriptPath -Raw
    $content = $content -replace '\$portableDir = "SharpShot-Release-v\d+\.\d+\.\d+\.\d+"', "`$portableDir = `"$releaseFolder`""
    $content = $content -replace '\$msiName = "SharpShot-\d+\.\d+\.\d+\.\d+\.msi"', "`$msiName = `"$msiName`""
    Update-FileContent $msiScriptPath $content
    Write-Host "  Updated build-msi.ps1" -ForegroundColor Gray
}

# 6. Build NSIS.bat
$nsisBatPath = Join-Path $ProjectDir "Build NSIS.bat"
if (Test-Path $nsisBatPath) {
    $content = Get-Content $nsisBatPath -Raw
    $content = $content -replace "SharpShot-Release-v\d+\.\d+\.\d+\.\d+", $releaseFolder
    Update-FileContent $nsisBatPath $content
    Write-Host "  Updated Build NSIS.bat" -ForegroundColor Gray
}

# 7. Build MSI.bat
$msiBatPath = Join-Path $ProjectDir "Build MSI.bat"
if (Test-Path $msiBatPath) {
    $content = Get-Content $msiBatPath -Raw
    $content = $content -replace "SharpShot-Release-v\d+\.\d+\.\d+\.\d+", $releaseFolder
    $content = $content -replace "SharpShot-\d+\.\d+\.\d+\.\d+\.msi", $msiName
    Update-FileContent $msiBatPath $content
    Write-Host "  Updated Build MSI.bat" -ForegroundColor Gray
}

# 8. 3xbuild.bat
$build3Path = Join-Path $ProjectDir "3xbuild.bat"
if (Test-Path $build3Path) {
    $content = Get-Content $build3Path -Raw
    $content = $content -replace 'set "PORTABLE_DIR=SharpShot-Release-v\d+\.\d+\.\d+\.\d+"', "set `"PORTABLE_DIR=$releaseFolder`""
    Update-FileContent $build3Path $content
    Write-Host "  Updated 3xbuild.bat" -ForegroundColor Gray
}

# 9. Package.appxmanifest
$appxPath = Join-Path $ProjectDir "Package.appxmanifest"
if (Test-Path $appxPath) {
    $content = Get-Content $appxPath -Raw
    $content = $content -replace 'Version="\d+\.\d+\.\d+\.\d+"', "Version=`"$version`""
    Update-FileContent $appxPath $content
    Write-Host "  Updated Package.appxmanifest" -ForegroundColor Gray
}

# 10. .gitignore (release folder pattern)
$gitignorePath = Join-Path $ProjectDir ".gitignore"
if (Test-Path $gitignorePath) {
    $content = Get-Content $gitignorePath -Raw
    $content = $content -replace '/SharpShot-Release-v\d+\.\d+\.\d+\.\d+', "/$releaseFolder"
    Update-FileContent $gitignorePath $content
    Write-Host "  Updated .gitignore" -ForegroundColor Gray
}

Write-Host "Version $version applied successfully." -ForegroundColor Green
