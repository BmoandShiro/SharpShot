# Test Windows Core Audio API enumeration
# This script will help us verify if the Windows Core Audio API is working properly

Write-Host "=== Testing Windows Core Audio API Enumeration ===" -ForegroundColor Green

# Test using PowerShell's built-in audio device enumeration
try {
    Write-Host "`nTesting PowerShell audio device enumeration..." -ForegroundColor Yellow
    
    # Get audio devices using Get-WmiObject (older method)
    $audioDevices = Get-WmiObject -Class Win32_SoundDevice -ErrorAction SilentlyContinue
    if ($audioDevices) {
        Write-Host "Found $($audioDevices.Count) audio devices via WMI:" -ForegroundColor Green
        foreach ($device in $audioDevices) {
            Write-Host "  - $($device.Name)" -ForegroundColor White
        }
    } else {
        Write-Host "No audio devices found via WMI" -ForegroundColor Red
    }
    
    # Test using Windows Media Foundation (if available)
    Write-Host "`nTesting Windows Media Foundation..." -ForegroundColor Yellow
    try {
        Add-Type -AssemblyName System.Windows.Forms
        $audioDevices = [System.Windows.Forms.SystemInformation]::AudioDevices
        if ($audioDevices) {
            Write-Host "Found audio devices via SystemInformation:" -ForegroundColor Green
            foreach ($device in $audioDevices) {
                Write-Host "  - $device" -ForegroundColor White
            }
        } else {
            Write-Host "No audio devices found via SystemInformation" -ForegroundColor Red
        }
    } catch {
        Write-Host "SystemInformation.AudioDevices not available: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    # Test using DirectShow (if available)
    Write-Host "`nTesting DirectShow enumeration..." -ForegroundColor Yellow
    try {
        $ffmpegPath = ".\ffmpeg\bin\ffmpeg.exe"
        if (Test-Path $ffmpegPath) {
            Write-Host "Testing FFmpeg DirectShow enumeration..." -ForegroundColor Green
            $dshowOutput = & $ffmpegPath -list_devices true -f dshow -i dummy 2>&1
            $dshowLines = $dshowOutput | Where-Object { $_ -match '"' }
            Write-Host "DirectShow devices found: $($dshowLines.Count)" -ForegroundColor Green
            foreach ($line in $dshowLines) {
                if ($line -match '"([^"]+)"') {
                    Write-Host "  - $($matches[1])" -ForegroundColor White
                }
            }
        } else {
            Write-Host "FFmpeg not found at $ffmpegPath" -ForegroundColor Red
        }
    } catch {
        Write-Host "DirectShow test failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test using WASAPI (if available)
    Write-Host "`nTesting WASAPI enumeration..." -ForegroundColor Yellow
    try {
        if (Test-Path $ffmpegPath) {
            Write-Host "Testing FFmpeg WASAPI enumeration..." -ForegroundColor Green
            $wasapiOutput = & $ffmpegPath -list_devices true -f wasapi -i dummy 2>&1
            $wasapiLines = $wasapiOutput | Where-Object { $_ -match '"' }
            Write-Host "WASAPI devices found: $($wasapiLines.Count)" -ForegroundColor Green
            foreach ($line in $wasapiLines) {
                if ($line -match '"([^"]+)"') {
                    Write-Host "  - $($matches[1])" -ForegroundColor White
                }
            }
        }
    } catch {
        Write-Host "WASAPI test failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error during audio device enumeration: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Green 