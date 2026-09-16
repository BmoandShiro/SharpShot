# Deprecated. Store packages are built with MakeAppx, not GenerateAppxPackageOnBuild.
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
Write-Host "build-simple-msix.ps1 now uses the Microsoft Store MakeAppx path." -ForegroundColor Yellow
& "$PSScriptRoot\build-store-no-obs.ps1" -Configuration $Configuration -SkipZip -NoPrompt
exit $LASTEXITCODE
