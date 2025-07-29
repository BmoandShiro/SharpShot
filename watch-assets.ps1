# SharpShot Asset File Watcher for Windows
# This script watches for asset file changes and can trigger actions

param(
    [string]$Action = "copy",
    [string]$SourcePath = ".",
    [string]$DestinationPath = "bin\Debug\net8.0-windows"
)

Write-Host "=== SharpShot Asset File Watcher ===" -ForegroundColor Cyan
Write-Host "Watching for asset file changes..." -ForegroundColor Yellow

# Create FileSystemWatcher for asset files
$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $SourcePath
$watcher.Filter = "*.png"
$watcher.IncludeSubdirectories = $true
$watcher.EnableRaisingEvents = $true

# Add more file types to watch
$additionalFilters = @("*.jpg", "*.jpeg", "*.ico", "*.gif", "*.bmp", "*.svg")

function Copy-AssetFile {
    param([string]$FilePath)
    
    try {
        $relativePath = $FilePath.Substring($SourcePath.Length + 1)
        $destinationFile = Join-Path $DestinationPath $relativePath
        
        # Create destination directory if it doesn't exist
        $destinationDir = Split-Path $destinationFile -Parent
        if (!(Test-Path $destinationDir)) {
            New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
        }
        
        # Copy the file
        Copy-Item $FilePath $destinationFile -Force
        Write-Host "✅ Copied: $relativePath" -ForegroundColor Green
        
        # If this is an icon or image file, trigger a rebuild
        if ($FilePath -match "\.(ico|png|jpg|jpeg)$") {
            Write-Host "🔄 Asset file changed - consider rebuilding..." -ForegroundColor Yellow
            Write-Host "   Run: dotnet build" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "❌ Error copying file: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Handle-FileChange {
    param([string]$FilePath, [string]$ChangeType)
    
    Write-Host "📁 $ChangeType detected: $FilePath" -ForegroundColor Cyan
    
    switch ($ChangeType) {
        "Created" { Copy-AssetFile $FilePath }
        "Changed" { Copy-AssetFile $FilePath }
        "Deleted" { 
            $relativePath = $FilePath.Substring($SourcePath.Length + 1)
            $destinationFile = Join-Path $DestinationPath $relativePath
            if (Test-Path $destinationFile) {
                Remove-Item $destinationFile -Force
                Write-Host "🗑️  Deleted: $relativePath" -ForegroundColor Yellow
            }
        }
    }
}

# Register event handlers
Register-ObjectEvent $watcher "Created" -Action { Handle-FileChange $Event.SourceEventArgs.FullPath "Created" }
Register-ObjectEvent $watcher "Changed" -Action { Handle-FileChange $Event.SourceEventArgs.FullPath "Changed" }
Register-ObjectEvent $watcher "Deleted" -Action { Handle-FileChange $Event.SourceEventArgs.FullPath "Deleted" }

# Set up watchers for additional file types
$additionalWatchers = @()
foreach ($filter in $additionalFilters) {
    $additionalWatcher = New-Object System.IO.FileSystemWatcher
    $additionalWatcher.Path = $SourcePath
    $additionalWatcher.Filter = $filter
    $additionalWatcher.IncludeSubdirectories = $true
    $additionalWatcher.EnableRaisingEvents = $true
    
    Register-ObjectEvent $additionalWatcher "Created" -Action { Handle-FileChange $Event.SourceEventArgs.FullPath "Created" }
    Register-ObjectEvent $additionalWatcher "Changed" -Action { Handle-FileChange $Event.SourceEventArgs.FullPath "Changed" }
    Register-ObjectEvent $additionalWatcher "Deleted" -Action { Handle-FileChange $Event.SourceEventArgs.FullPath "Deleted" }
    
    $additionalWatchers += $additionalWatcher
}

Write-Host "👀 Watching for changes in:" -ForegroundColor Green
Write-Host "  - Image files (*.png, *.jpg, *.jpeg, *.ico, *.gif, *.bmp, *.svg)" -ForegroundColor Gray
Write-Host "  - Source directory: $SourcePath" -ForegroundColor Gray
Write-Host "  - Destination: $DestinationPath" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C to stop watching..." -ForegroundColor Yellow

try {
    # Keep the script running
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
finally {
    # Clean up
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
    
    foreach ($additionalWatcher in $additionalWatchers) {
        $additionalWatcher.EnableRaisingEvents = $false
        $additionalWatcher.Dispose()
    }
    
    Write-Host "Asset watcher stopped." -ForegroundColor Yellow
} 