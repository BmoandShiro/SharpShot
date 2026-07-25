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

        /// <summary>
        /// Resolves OBS executable path. Prefer a valid user-linked path, then auto-detect.
        /// </summary>
        public static string FindOBSPath(string? linkedObsPath = null)
        {
            if (!string.IsNullOrWhiteSpace(linkedObsPath) && File.Exists(linkedObsPath))
            {
                return linkedObsPath;
            }

            var possiblePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "OBS-Studio", "bin", "64bit", "obs64.exe"),
                Path.Combine(AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe"),
                Path.Combine(Directory.GetParent(AppContext.BaseDirectory)?.FullName ?? AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SharpShot", "OBS-Studio", "bin", "64bit", "obs64.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "64bit", "obs64.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "64bit", "obs64.exe"),
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

            var obsProcesses = Process.GetProcessesByName("obs64");
            if (obsProcesses.Length == 0)
            {
                obsProcesses = Process.GetProcessesByName("obs32");
            }

            if (obsProcesses.Length > 0)
            {
                try
                {
                    var obsPath = obsProcesses[0].MainModule?.FileName;
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

        public static bool IsValidLinkedObsPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            var name = Path.GetFileName(path);
            return name.Equals("obs64.exe", StringComparison.OrdinalIgnoreCase)
                || name.Equals("obs32.exe", StringComparison.OrdinalIgnoreCase);
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

        public static async Task<string> GetOBSVersionAsync(string? linkedObsPath = null)
        {
            try
            {
                var obsPath = FindOBSPath(linkedObsPath);
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

        public static string GetOBSInstallationInstructions()
        {
            return @"OBS Studio Installation Instructions:

1. Download OBS Studio from: https://obsproject.com/
2. Install OBS Studio with default settings
3. Link OBS in SharpShot Settings (Recording → Linked OBS)

OBS Integration Benefits:
- Launch OBS from the SharpShot recording toolbar
- Use OBS's own UI for professional recording and streaming

Note: SharpShot no longer requires a bundled OBS copy; link your installed OBS instead.";
        }
    }
}
