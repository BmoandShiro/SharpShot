# SharpShot Force Refresh Script
# This script can force the application to refresh resources or restart

param(
    [Parameter(Position=0)]
    [ValidateSet("refresh", "restart", "watch")]
    [string]$Command = "watch"
)

Write-Host "=== SharpShot Force Refresh Tool ===" -ForegroundColor Cyan

function Force-RefreshResources {
    Write-Host "🔄 Forcing resource refresh..." -ForegroundColor Yellow
    
    # Get the SharpShot process
    $process = Get-Process -Name "SharpShot" -ErrorAction SilentlyContinue
    
    if ($process) {
        Write-Host "Found SharpShot process (PID: $($process.Id))" -ForegroundColor Green
        
        # Send a custom message to refresh resources
        # This would require the app to handle this message
        Write-Host "Sending refresh signal to application..." -ForegroundColor Yellow
        
        # Alternative: Restart the application
        Write-Host "Restarting application to apply changes..." -ForegroundColor Yellow
        Stop-Process -Name "SharpShot" -Force
        Start-Sleep -Seconds 1
        dotnet run
    } else {
        Write-Host "SharpShot is not running. Starting it now..." -ForegroundColor Yellow
        dotnet run
    }
}

function Restart-Application {
    Write-Host "🔄 Restarting SharpShot application..." -ForegroundColor Yellow
    
    # Stop any running instances
    $processes = Get-Process -Name "SharpShot" -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Host "Stopping existing SharpShot processes..." -ForegroundColor Yellow
        Stop-Process -Name "SharpShot" -Force
        Start-Sleep -Seconds 2
    }
    
    # Start the application
    Write-Host "Starting SharpShot..." -ForegroundColor Yellow
    dotnet run
}

function Watch-And-Refresh {
    Write-Host "👀 Watching for XAML file changes..." -ForegroundColor Yellow
    Write-Host "Press Ctrl+C to stop watching" -ForegroundColor Gray
    
    # Create FileSystemWatcher for XAML files
    $watcher = New-Object System.IO.FileSystemWatcher
    $watcher.Path = "."
    $watcher.Filter = "*.xaml"
    $watcher.IncludeSubdirectories = $true
    $watcher.EnableRaisingEvents = $true
    
    # Register event handlers
    Register-ObjectEvent $watcher "Changed" -Action {
        $filePath = $Event.SourceEventArgs.FullPath
        $fileName = Split-Path $filePath -Leaf
        
        Write-Host "📁 XAML file changed: $fileName" -ForegroundColor Cyan
        Write-Host "🔄 Restarting application to apply changes..." -ForegroundColor Yellow
        
        # Stop current process
        $processes = Get-Process -Name "SharpShot" -ErrorAction SilentlyContinue
        if ($processes) {
            Stop-Process -Name "SharpShot" -Force
            Start-Sleep -Seconds 1
        }
        
        # Start new process
        Start-Job -ScriptBlock {
            Set-Location $using:PSScriptRoot
            dotnet run
        } | Out-Null
        
        Write-Host "✅ Application restarted!" -ForegroundColor Green
    }
    
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
        Write-Host "File watcher stopped." -ForegroundColor Yellow
    }
}

# Execute the requested command
switch ($Command) {
    "refresh" { Force-RefreshResources }
    "restart" { Restart-Application }
    "watch" { Watch-And-Refresh }
    default { 
        Write-Host "Unknown command: $Command" -ForegroundColor Red
        Write-Host "Available commands: refresh, restart, watch" -ForegroundColor Yellow
    }
} 