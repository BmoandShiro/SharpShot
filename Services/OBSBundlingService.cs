using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace SharpShot.Services
{
    public class OBSBundlingService
    {
        private readonly string _obsInstallPath;
        private readonly string _obsExtractPath;

        public OBSBundlingService()
        {
            // Set up paths for bundled OBS installation
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _obsInstallPath = Path.Combine(appDataPath, "SharpShot", "OBS-Studio");
            _obsExtractPath = Path.Combine(appDataPath, "SharpShot", "OBS-Extract");
            
            // Ensure directories exist
            Directory.CreateDirectory(_obsInstallPath);
            Directory.CreateDirectory(_obsExtractPath);
        }

        public async Task<bool> InstallOBSAsync()
        {
            try
            {
                LogToFile("Starting bundled OBS installation process...");

                // Check if OBS is already installed
                if (await IsOBSInstalledAsync())
                {
                    LogToFile("Bundled OBS is already installed");
                    return true;
                }

                // Extract bundled OBS
                if (!await ExtractBundledOBSAsync())
                {
                    LogToFile("Failed to extract bundled OBS");
                    return false;
                }

                // Configure OBS WebSocket server
                if (!await ConfigureOBSWebSocketAsync())
                {
                    LogToFile("Failed to configure OBS WebSocket");
                    return false;
                }

                LogToFile("Bundled OBS installation completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogToFile($"Error during bundled OBS installation: {ex.Message}");
                return false;
            }
        }

        private Task<bool> IsOBSInstalledAsync()
        {
            try
            {
                // Check multiple possible bundled locations
                var possibleBundledPaths = new[]
                {
                    // Current directory + OBS-Studio
                    Path.Combine(Directory.GetCurrentDirectory(), "OBS-Studio", "bin", "64bit", "obs64.exe"),
                    // App base directory + OBS-Studio
                    Path.Combine(AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe"),
                    // Parent of app base directory + OBS-Studio (in case we're in bin folder)
                    Path.Combine(Directory.GetParent(AppContext.BaseDirectory)?.FullName ?? AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe"),
                    // Go up one more level if needed
                    Path.Combine(Directory.GetParent(Directory.GetParent(AppContext.BaseDirectory)?.FullName ?? AppContext.BaseDirectory)?.FullName ?? AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe")
                };
                
                foreach (var bundledPath in possibleBundledPaths)
                {
                    LogToFile($"Checking bundled path: {bundledPath}");
                    if (File.Exists(bundledPath))
                    {
                        LogToFile($"Found OBS bundled at: {bundledPath}");
                        return Task.FromResult(true);
                    }
                }
                
                // Check if OBS is installed in user's app data (extracted from bundle)
                var userAppDataPath = Path.Combine(_obsInstallPath, "bin", "64bit", "obs64.exe");
                LogToFile($"Checking user app data path: {userAppDataPath}");
                if (File.Exists(userAppDataPath))
                {
                    LogToFile("Found OBS in user app data");
                    return Task.FromResult(true);
                }
                
                // Check system installation paths as fallback
                var systemPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "64bit", "obs64.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "64bit", "obs64.exe")
                };
                
                foreach (var path in systemPaths)
                {
                    LogToFile($"Checking system path: {path}");
                    if (File.Exists(path))
                    {
                        LogToFile($"Found OBS in system path: {path}");
                        return Task.FromResult(true);
                    }
                }
                
                LogToFile("OBS not found in any location");
                LogToFile($"Current directory: {Directory.GetCurrentDirectory()}");
                LogToFile($"App base directory: {AppContext.BaseDirectory}");
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                LogToFile($"Error checking OBS installation: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        private async Task<bool> ExtractBundledOBSAsync()
        {
            try
            {
                LogToFile("Extracting bundled OBS Studio...");

                // Show progress to user
                MessageBox.Show(
                    "Setting up OBS Studio for enhanced audio recording...\n\n" +
                    "This will extract the bundled OBS Studio installation.",
                    "Setup OBS Studio",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Check if we have bundled OBS files
                var bundledOBSDir = Path.Combine(GetApplicationDirectory(), "OBS-Studio");
                if (!Directory.Exists(bundledOBSDir))
                {
                    LogToFile("Bundled OBS directory not found, falling back to download");
                    return await DownloadAndInstallOBSAsync();
                }

                // Copy bundled OBS to user's app data
                await CopyDirectoryAsync(bundledOBSDir, _obsInstallPath);
                
                LogToFile($"Bundled OBS extracted to: {_obsInstallPath}");
                return true;
            }
            catch (Exception ex)
            {
                LogToFile($"Error extracting bundled OBS: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DownloadAndInstallOBSAsync()
        {
            try
            {
                LogToFile("Bundled OBS not found, downloading...");

                var result = MessageBox.Show(
                    "OBS Studio needs to be downloaded for enhanced audio recording.\n\n" +
                    "This will download approximately 100MB. Continue?",
                    "Download OBS Studio",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    LogToFile("User cancelled OBS download");
                    return false;
                }

                // Download OBS installer
                var downloadUrl = "https://github.com/obsproject/obs-studio/releases/download/30.1.2/OBS-Studio-30.1.2-Full-Installer-x64.exe";
                var installerPath = Path.Combine(_obsExtractPath, "obs-installer.exe");

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(10);
                    var response = await httpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();

                    using (var fileStream = File.Create(installerPath))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }
                }

                // Install OBS silently
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = installerPath,
                        Arguments = $"/S /D={_obsInstallPath}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    LogToFile("OBS installer completed successfully");
                    return true;
                }
                else
                {
                    LogToFile($"OBS installer failed with exit code: {process.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error downloading/installing OBS: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ConfigureOBSWebSocketAsync()
        {
            try
            {
                LogToFile("Configuring OBS WebSocket server...");

                // Find OBS executable
                var obsPath = Path.Combine(_obsInstallPath, "bin", "64bit", "obs64.exe");
                if (!File.Exists(obsPath))
                {
                    LogToFile("OBS executable not found for configuration");
                    return false;
                }

                // Start OBS to create configuration files
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = obsPath,
                        Arguments = "--minimize-to-tray --websocket_port 4444",
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                };

                process.Start();
                
                // Wait a bit for OBS to start and create config files
                await Task.Delay(5000);

                // Stop OBS
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Ignore if already closed
                }

                LogToFile("OBS WebSocket configuration completed");
                return true;
            }
            catch (Exception ex)
            {
                LogToFile($"Error configuring OBS WebSocket: {ex.Message}");
                return false;
            }
        }

        public string GetOBSInstallPath()
        {
            // Check multiple possible bundled locations
            var possibleBundledPaths = new[]
            {
                // Current directory + OBS-Studio
                Path.Combine(Directory.GetCurrentDirectory(), "OBS-Studio", "bin", "64bit", "obs64.exe"),
                // App base directory + OBS-Studio
                Path.Combine(AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe"),
                // Parent of app base directory + OBS-Studio (in case we're in bin folder)
                Path.Combine(Directory.GetParent(AppContext.BaseDirectory)?.FullName ?? AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe"),
                // Go up one more level if needed
                Path.Combine(Directory.GetParent(Directory.GetParent(AppContext.BaseDirectory)?.FullName ?? AppContext.BaseDirectory)?.FullName ?? AppContext.BaseDirectory, "OBS-Studio", "bin", "64bit", "obs64.exe")
            };
            
            foreach (var bundledPath in possibleBundledPaths)
            {
                if (File.Exists(bundledPath))
                {
                    return bundledPath;
                }
            }
            
            // Then check if OBS is installed in user's app data (extracted from bundle)
            var userAppDataPath = Path.Combine(_obsInstallPath, "bin", "64bit", "obs64.exe");
            if (File.Exists(userAppDataPath))
            {
                return userAppDataPath;
            }

            // Finally check system installation paths
            var systemPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "64bit", "obs64.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "64bit", "obs64.exe")
            };

            foreach (var path in systemPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private string GetApplicationDirectory()
        {
            // Use AppContext.BaseDirectory for single-file apps
            return AppContext.BaseDirectory;
        }

        private async Task CopyDirectoryAsync(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                await CopyDirectoryAsync(subDir.FullName, newDestinationDir);
            }
        }

        private void LogToFile(string message)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "obs_bundling.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] [OBSBundlingService] {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
} 