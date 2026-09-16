# Microsoft Store MSIX entry point.
# GenerateAppxPackageOnBuild does not produce a Store-ready package for this WPF project.
# This script delegates to the MakeAppx path in build-store-no-obs.ps1.

param(
    [string]$Configuration = "Release",
    [switch]$Clean,
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean SharpShot.csproj --configuration $Configuration
}

& "$PSScriptRoot\build-store-no-obs.ps1" -Configuration $Configuration -SkipZip -NoPrompt
exit $LASTEXITCODE
