using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using SharpShot;

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

            var possiblePaths = new List<string>();

            // Steam and Store builds must not treat a leftover OBS-Studio folder next to the exe
            // (or an AppData extract from the GitHub downloader) as "our" bundled copy.
            if (!BuildInfo.DisableInAppUpdates)
            {
                possiblePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "OBS-Studio", "bin", "64bit", "obs64.exe"));
                possiblePaths.Add(Path.Combine(AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe"));
                possiblePaths.Add(Path.Combine(Directory.GetParent(AppContext.BaseDirectory)?.FullName ?? AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe"));
                possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SharpShot", "OBS-Studio", "bin", "64bit", "obs64.exe"));
            }

            possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "64bit", "obs64.exe"));
            possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "64bit", "obs64.exe"));

            if (BuildInfo.IsSteam)
                possiblePaths.AddRange(GetSteamLibraryObsCandidates());

            possiblePaths.Add("obs64.exe");
            possiblePaths.Add("obs32.exe");

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

        /// <summary>
        /// OBS Studio installed via Steam (app 1905180). Searches the Steam install plus extra libraries in libraryfolders.vdf.
        /// </summary>
        private static IEnumerable<string> GetSteamLibraryObsCandidates()
        {
            foreach (var library in GetSteamLibraryRoots())
            {
                yield return Path.Combine(library, "steamapps", "common", "OBS Studio", "bin", "64bit", "obs64.exe");
                yield return Path.Combine(library, "steamapps", "common", "OBS-Studio", "bin", "64bit", "obs64.exe");
            }
        }

        private static IEnumerable<string> GetSteamLibraryRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var steam = GetSteamInstallPath();
            if (!string.IsNullOrEmpty(steam) && seen.Add(steam))
                yield return steam;

            if (string.IsNullOrEmpty(steam))
                yield break;

            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                yield break;

            string text;
            try
            {
                text = File.ReadAllText(vdf);
            }
            catch
            {
                yield break;
            }

            foreach (Match match in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
            {
                var path = match.Groups[1].Value.Replace("\\\\", "\\").Replace('/', '\\');
                if (!string.IsNullOrWhiteSpace(path) && seen.Add(path))
                    yield return path;
            }
        }

        private static string? GetSteamInstallPath()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                var path = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    path = path.Replace('/', '\\');
                    if (Directory.Exists(path))
                        return path;
                }
            }
            catch
            {
                // ignore
            }

            var pf86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            if (Directory.Exists(pf86))
                return pf86;

            var pf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam");
            if (Directory.Exists(pf))
                return pf;

            return null;
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
