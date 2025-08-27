# SharpShot Runtime DLL Downloader
# Downloads essential Visual C++ runtime DLLs for bundling

Write-Host "=== SharpShot Runtime DLL Downloader ===" -ForegroundColor Green
Write-Host "Downloading essential Visual C++ runtime DLLs..." -ForegroundColor Yellow

$ErrorActionPreference = "Continue"
$runtimesDir = "runtimes\win-x64\native"

# Create runtimes directory if it doesn't exist
if (!(Test-Path $runtimesDir)) {
    New-Item -ItemType Directory -Path $runtimesDir -Force | Out-Null
    Write-Host "Created runtimes directory: $runtimesDir" -ForegroundColor Green
}

# Function to download a file
function Download-File {
    param(
        [string]$Url,
        [string]$OutputPath,
        [string]$Description
    )
    
    try {
        Write-Host "Downloading $Description..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri $Url -OutFile $OutputPath -UseBasicParsing
        Write-Host "✓ Downloaded $Description" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "✗ Failed to download $Description`: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Essential Visual C++ runtime DLLs - ALL 4 that commonly cause issues
$dlls = @(
    @{
        Name = "MSVCP140.dll"
        Description = "Microsoft Visual C++ Runtime Library (MSVCP140)"
        Source = "https://github.com/microsoft/VisualStudio/raw/main/src/vctools/redist/x64/14.16.27012/msvcp140.dll"
    },
    @{
        Name = "VCRUNTIME140.dll"
        Description = "Microsoft Visual C++ Runtime Library (VCRUNTIME140)"
        Source = "https://github.com/microsoft/VisualStudio/raw/main/src/vctools/redist/x64/14.16.27012/vcruntime140.dll"
    },
    @{
        Name = "VCRUNTIME140_1.dll"
        Description = "Microsoft Visual C++ Runtime Library (VCRUNTIME140_1)"
        Source = "https://github.com/microsoft/VisualStudio/raw/main/src/vctools/redist/x64/14.16.27012/vcruntime140_1.dll"
    },
    @{
        Name = "MSVCP140_1.dll"
        Description = "Microsoft Visual C++ Runtime Library (MSVCP140_1)"
        Source = "https://github.com/microsoft/VisualStudio/raw/main/src/vctools/redist/x64/14.16.27012/msvcp140_1.dll"
    }
)

$successCount = 0
$totalCount = $dlls.Count

foreach ($dll in $dlls) {
    $outputPath = Join-Path $runtimesDir $dll.Name
    
    # Skip if file already exists
    if (Test-Path $outputPath) {
        Write-Host "✓ $($dll.Name) already exists, skipping..." -ForegroundColor Green
        $successCount++
        continue
    }
    
    if (Download-File -Url $dll.Source -OutputPath $outputPath -Description $dll.Description) {
        $successCount++
    }
}

Write-Host "`n=== DOWNLOAD SUMMARY ===" -ForegroundColor Green
Write-Host "Successfully downloaded: $successCount/$totalCount DLLs" -ForegroundColor $(if ($successCount -eq $totalCount) { "Green" } else { "Yellow" })

if ($successCount -eq $totalCount) {
    Write-Host "`n✓ All essential runtime DLLs downloaded successfully!" -ForegroundColor Green
    Write-Host "These DLLs will now be bundled with your SharpShot application." -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "1. Build your application: dotnet build --configuration Release" -ForegroundColor White
    Write-Host "2. The DLLs will be automatically included in the output" -ForegroundColor White
    Write-Host "3. Users won't need to install Visual C++ Redistributable separately" -ForegroundColor White
}
else {
    Write-Host "`n⚠ Some DLLs failed to download." -ForegroundColor Yellow
    Write-Host "You may need to manually download them or install Visual C++ Redistributable." -ForegroundColor Yellow
}

Write-Host "`n=== END ===" -ForegroundColor Green
