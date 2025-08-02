# Test Windows Core Audio API enumeration
Write-Host "=== Testing Windows Core Audio API Enumeration ==="

# Test using PowerShell to enumerate audio devices
try {
    Write-Host "Enumerating audio devices using PowerShell..."
    
    # Get audio devices using Windows Core Audio API via PowerShell
    $audioDevices = Get-WmiObject -Class Win32_SoundDevice | Select-Object Name, DeviceID, Status
    
    Write-Host "Found $($audioDevices.Count) audio devices:"
    foreach ($device in $audioDevices) {
        Write-Host "  - $($device.Name) (Status: $($device.Status))"
    }
    
    # Also try using the Windows Core Audio API directly
    Write-Host "`nTesting direct Windows Core Audio API..."
    
    Add-Type -TypeDefinition @"
    using System;
    using System.Runtime.InteropServices;
    
    public class AudioDeviceEnumerator {
        [DllImport("ole32.dll")]
        public static extern int CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid riid, out IntPtr ppv);
        
        [DllImport("ole32.dll")]
        public static extern int CoInitialize(IntPtr pvReserved);
        
        [DllImport("ole32.dll")]
        public static extern void CoUninitialize();
        
        public static readonly Guid MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        public static readonly Guid IMMDeviceEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
    }
"@
    
    Write-Host "Windows Core Audio API types loaded successfully"
    
} catch {
    Write-Host "Error testing audio enumeration: $($_.Exception.Message)"
}

Write-Host "`nTest completed. Check the output above for device enumeration results." 