# SharpShot Runtime DLL Finder
# Finds Visual C++ runtime DLLs on your system

Write-Host "=== SharpShot Runtime DLL Finder ===" -ForegroundColor Green
Write-Host "Searching for Visual C++ runtime DLLs on your system..." -ForegroundColor Yellow

$ErrorActionPreference = "Continue"
$runtimesDir = "runtimes\win-x64\native"

# Create runtimes directory if it doesn't exist
if (!(Test-Path $runtimesDir)) {
    New-Item -ItemType Directory -Path $runtimesDir -Force | Out-Null
    Write-Host "Created runtimes directory: $runtimesDir" -ForegroundColor Green
}

# DLLs we need to find
$dlls = @(
    "MSVCP140.dll",
    "VCRUNTIME140.dll", 
    "VCRUNTIME140_1.dll",
    "MSVCP140_1.dll"
)

Write-Host "`nSearching for these DLLs:" -ForegroundColor Cyan
foreach ($dll in $dlls) {
    Write-Host "  - $dll" -ForegroundColor White
}

Write-Host "`n=== SEARCHING SYSTEM PATHS ===" -ForegroundColor Green

# Common locations where these DLLs are found
$searchPaths = @(
    "$env:SystemRoot\System32",
    "$env:SystemRoot\SysWOW64",
    "$env:ProgramFiles\Common Files\Microsoft Shared\VC\redist\x64",
    "$env:ProgramFiles\Common Files\Microsoft Shared\VC\redist\x86",
    "$env:ProgramFiles (x86)\Common Files\Microsoft Shared\VC\redist\x64",
    "$env:ProgramFiles (x86)\Common Files\Microsoft Shared\VC\redist\x86",
    "$env:ProgramFiles\Microsoft Visual Studio\2022\BuildTools\VC\Redist\MSVC\*\x64\Microsoft.VC142.CRT",
    "$env:ProgramFiles\Microsoft Visual Studio\2022\Community\VC\Redist\MSVC\*\x64\Microsoft.VC142.CRT",
    "$env:ProgramFiles\Microsoft Visual Studio\2022\Professional\VC\Redist\MSVC\*\x64\Microsoft.VC142.CRT",
    "$env:ProgramFiles\Microsoft Visual Studio\2022\Enterprise\VC\Redist\MSVC\*\x64\Microsoft.VC142.CRT"
)

$foundDlls = @{}

foreach ($dll in $dlls) {
    Write-Host "`nSearching for $dll..." -ForegroundColor Cyan
    $found = $false
    
    foreach ($path in $searchPaths) {
        # Expand wildcards in paths
        $expandedPaths = Get-ChildItem -Path $path -ErrorAction SilentlyContinue | Where-Object { $_.PSIsContainer }
        
        if ($expandedPaths) {
            foreach ($expandedPath in $expandedPaths) {
                $fullPath = Join-Path $expandedPath.FullName $dll
                if (Test-Path $fullPath) {
                    Write-Host "  ✓ Found at: $fullPath" -ForegroundColor Green
                    $foundDlls[$dll] = $fullPath
                    $found = $true
                    break
                }
            }
        } else {
            # Direct path without wildcards
            $fullPath = Join-Path $path $dll
            if (Test-Path $fullPath) {
                Write-Host "  ✓ Found at: $fullPath" -ForegroundColor Green
                $foundDlls[$dll] = $fullPath
                $found = $true
                break
            }
        }
    }
    
    if (-not $found) {
        Write-Host "  ✗ NOT FOUND" -ForegroundColor Red
    }
}

Write-Host "`n=== COPY INSTRUCTIONS ===" -ForegroundColor Green

if ($foundDlls.Count -eq $dlls.Count) {
    Write-Host "✓ All DLLs found! Here's how to copy them:" -ForegroundColor Green
    Write-Host ""
    Write-Host "1. Copy these files to: $((Get-Location).Path)\$runtimesDir" -ForegroundColor Cyan
    Write-Host ""
    
    foreach ($dll in $dlls) {
        if ($foundDlls.ContainsKey($dll)) {
            Write-Host "   $dll" -ForegroundColor White
            Write-Host "     FROM: $($foundDlls[$dll])" -ForegroundColor Yellow
            Write-Host "     TO:   $((Get-Location).Path)\$runtimesDir\$dll" -ForegroundColor Yellow
            Write-Host ""
        }
    }
    
    Write-Host "2. You can copy them manually or use this command:" -ForegroundColor Cyan
    Write-Host "   Copy-Item '$($foundDlls[$dlls[0]])' '$runtimesDir\$($dlls[0])'" -ForegroundColor White
    Write-Host ""
    Write-Host "3. After copying, build your app: dotnet build --configuration Release" -ForegroundColor Cyan
} else {
    Write-Host "⚠ Only found $($foundDlls.Count) out of $($dlls.Count) DLLs" -ForegroundColor Yellow
    Write-Host "You may need to install Visual C++ Redistributable or search additional locations." -ForegroundColor Yellow
}

Write-Host "`n=== END ===" -ForegroundColor Green
