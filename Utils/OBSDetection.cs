using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SharpShot.Utils
{
    public static class OBSDetection
    {
        public static Task<bool> IsOBSInstalledAsync()
        {
            try
            {
                var obsPath = FindOBSPath();
                return Task.FromResult(!string.IsNullOrEmpty(obsPath));
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public static string FindOBSPath()
        {
            // Check multiple possible locations for OBS (same as OBSRecordingService)
            var possiblePaths = new[]
            {
                // Current directory (for Docker mounted OBS)
                Path.Combine(Directory.GetCurrentDirectory(), "OBS-Studio", "bin", "64bit", "obs64.exe"),
                // App base directory
                Path.Combine(AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe"),
                // Parent of app base directory
                Path.Combine(Directory.GetParent(AppContext.BaseDirectory)?.FullName ?? AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe"),
                // User app data (extracted bundle)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SharpShot", "OBS-Studio", "bin", "64bit", "obs64.exe"),
                // System installations
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "64bit", "obs64.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "64bit", "obs64.exe"),
                // Direct executable names
                "obs64.exe",
                "obs32.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Check if OBS is already running and get its path
            var obsProcesses = Process.GetProcessesByName("obs64");
            if (obsProcesses.Length > 0)
            {
                try
                {
                    var obsProcess = obsProcesses[0];
                    var obsPath = obsProcess.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(obsPath) && File.Exists(obsPath))
                    {
                        return obsPath;
                    }
                }
                catch
                {
                    // Ignore errors getting process path
                }
            }

            return string.Empty;
        }

        public static Task<bool> IsOBSRunningAsync()
        {
            try
            {
                var processes = Process.GetProcessesByName("obs64");
                if (processes.Length == 0)
                {
                    processes = Process.GetProcessesByName("obs32");
                }
                return Task.FromResult(processes.Length > 0);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public static async Task<string> GetOBSVersionAsync()
        {
            try
            {
                var obsPath = FindOBSPath();
                if (string.IsNullOrEmpty(obsPath))
                {
                    return "OBS not found";
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = obsPath,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit();

                return output.Trim();
            }
            catch (Exception ex)
            {
                return $"Error getting OBS version: {ex.Message}";
            }
        }

        public static async Task<bool> TestOBSWebSocketAsync()
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                // Try the correct port (4455) that OBSRecordingService uses
                var response = await httpClient.GetAsync("http://localhost:4455/api/version");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static string GetOBSInstallationInstructions()
        {
            return @"OBS Studio Installation Instructions:

1. Download OBS Studio from: https://obsproject.com/
2. Install OBS Studio with default settings
3. Open OBS Studio and go to Tools > WebSocket Server Settings
4. Enable WebSocket server and set port to 4444
5. Set a password (optional) or leave blank for no password
6. Click OK to save settings
7. Restart SharpShot

OBS Integration Benefits:
- Superior audio recording quality
- Advanced audio mixing capabilities
- Professional-grade audio filters
- Better device management
- Real-time audio monitoring

Note: OBS must be running for the integration to work.";
        }
    }
} 