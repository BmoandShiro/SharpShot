# Signs a copy of the Store MSIX so it can be sideloaded for testing.
# Do not upload the signed copy. Partner Center wants the unsigned .msix and re-signs it.
# The test certificate subject must match Package.appxmanifest Publisher.

param(
    [string]$MsixPath = ""
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

function Find-SignTool {
    $kitRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (Test-Path $kitRoot) {
        $found = Get-ChildItem -Path $kitRoot -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

if (-not $MsixPath) {
    $MsixPath = Get-ChildItem -Path "bin" -Filter "SharpShot-Store-v*.msix" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike "*.sideload.msix" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $MsixPath -or -not (Test-Path $MsixPath)) {
    Write-Host "No unsigned Store MSIX found. Run Build MSIX for Store.bat first." -ForegroundColor Red
    exit 1
}

$signTool = Find-SignTool
if (-not $signTool) {
    Write-Host "signtool.exe not found. Install the Windows SDK." -ForegroundColor Red
    exit 1
}

$publisher = "CN=7E8A6D64-EB7B-4C99-95F4-1194CC95BBA8"
$existing = Get-ChildItem Cert:\CurrentUser\My | Where-Object {
    $_.Subject -eq $publisher -and $_.FriendlyName -eq "SharpShot Local Test"
} | Select-Object -First 1

if (-not $existing) {
    Write-Host "Creating a local test certificate matching the Store publisher id..." -ForegroundColor Yellow
    $existing = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $publisher `
        -KeyUsage DigitalSignature `
        -FriendlyName "SharpShot Local Test" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
        -NotAfter (Get-Date).AddYears(3)
}

$trust = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPeople", "CurrentUser")
$trust.Open("ReadWrite")
$alreadyTrusted = $trust.Certificates | Where-Object { $_.Thumbprint -eq $existing.Thumbprint }
if (-not $alreadyTrusted) {
    $trust.Add($existing)
    Write-Host "Trusted the test certificate for sideloading." -ForegroundColor Green
}
$trust.Close()

$signed = [System.IO.Path]::ChangeExtension($MsixPath, $null).TrimEnd('.') + ".sideload.msix"
Copy-Item $MsixPath $signed -Force

Write-Host "Signing $signed" -ForegroundColor Yellow
& $signTool sign /fd SHA256 /sha1 $existing.Thumbprint $signed
if ($LASTEXITCODE -ne 0) {
    Write-Host "signtool failed." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Local test package:" -ForegroundColor Green
Write-Host "  $signed" -ForegroundColor Cyan
Write-Host ""
Write-Host "Windows will still refuse the install until this test certificate is trusted as a root." -ForegroundColor White
Write-Host "Run these once, then the install command:" -ForegroundColor White
Write-Host "  `$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { `$_.FriendlyName -eq 'SharpShot Local Test' } | Select-Object -First 1" -ForegroundColor Cyan
Write-Host "  `$root = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root','CurrentUser')" -ForegroundColor Cyan
Write-Host "  `$root.Open('ReadWrite'); `$root.Add(`$cert); `$root.Close()" -ForegroundColor Cyan
Write-Host "  Add-AppxPackage -Path `"$signed`"" -ForegroundColor Cyan
Write-Host "Upload the unsigned file to Partner Center, not this signed copy." -ForegroundColor Yellow
