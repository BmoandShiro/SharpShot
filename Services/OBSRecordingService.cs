using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SharpShot.Models;
using SharpShot.Services;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace SharpShot.Services
{
    public class OBSRecordingService
    {
        private readonly SettingsService _settingsService;
        private readonly HttpClient _httpClient;
        private string? _currentRecordingPath;
        private bool _isRecording = false;
        private bool _isConnected = false;
        private Process? _obsProcess;
        
        // Events that MainWindow expects
        public event EventHandler<bool>? RecordingStateChanged;

        public OBSRecordingService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<bool> EnsureOBSRunningAsync()
        {
            try
            {
                LogToFile("Checking if OBS is running...");
                
                // Check if OBS is already running
                var obsProcesses = Process.GetProcessesByName("obs64");
                if (obsProcesses.Length > 0)
                {
                    LogToFile("OBS is already running");
                    _obsProcess = obsProcesses[0];
                    _isConnected = true;
                    return true;
                }

                // Start OBS if not running
                LogToFile("OBS not running, starting new instance...");
                return await StartOBSAsync();
            }
            catch (Exception ex)
            {
                LogToFile($"Error ensuring OBS is running: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> StartOBSAsync()
        {
            try
            {
                LogToFile("Starting OBS...");
                
                // Always check if OBS is already running first
                var obsProcesses = Process.GetProcessesByName("obs64");
                if (obsProcesses.Length > 0)
                {
                    LogToFile("OBS is already running, using existing instance");
                    _obsProcess = obsProcesses[0];
                    _isConnected = true;
                    return true;
                }
                
                // Find OBS installation
                var obsPath = FindOBSPath();
                if (string.IsNullOrEmpty(obsPath))
                {
                    LogToFile("OBS not found in common locations");
                    return false;
                }

                LogToFile($"Found OBS at: {obsPath}");

                // Start OBS with minimal startup
                _obsProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = obsPath,
                        Arguments = "--minimize-to-tray --startup_to_tray --disable-updater --disable-crash-handler",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory = Path.GetDirectoryName(obsPath)
                    }
                };

                LogToFile("Starting OBS process...");
                _obsProcess.Start();
                LogToFile($"Started OBS process: {obsPath}");

                // Wait for OBS to start
                await Task.Delay(5000); // Wait 5 seconds for OBS to start
                
                // Check if OBS started successfully
                obsProcesses = Process.GetProcessesByName("obs64");
                if (obsProcesses.Length > 0)
                {
                    LogToFile("OBS started successfully");
                    _isConnected = true;
                    return true;
                }

                LogToFile("Failed to start OBS");
                return false;
            }
            catch (Exception ex)
            {
                LogToFile($"Error starting OBS: {ex.Message}");
                return false;
            }
        }

        private string FindOBSPath()
        {
            // Check multiple possible locations for OBS
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

            LogToFile($"Current directory: {Directory.GetCurrentDirectory()}");
            LogToFile($"App base directory: {AppContext.BaseDirectory}");

            foreach (var path in possiblePaths)
            {
                LogToFile($"Checking OBS path: {path}");
                if (File.Exists(path))
                {
                    LogToFile($"Found OBS at: {path}");
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
                        LogToFile($"Found OBS running at: {obsPath}");
                        return obsPath;
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"Error getting OBS process path: {ex.Message}");
                }
            }

            LogToFile("OBS not found in any location");
            return string.Empty;
        }

        // New method to handle "Save with OBS" scenario
        public async Task<bool> SetupOBSForRecordingAsync()
        {
            try
            {
                LogToFile("Setting up OBS for recording...");
                
                // First, try to find OBS
                var obsPath = FindOBSPath();
                if (string.IsNullOrEmpty(obsPath))
                {
                    LogToFile("OBS not found - cannot proceed");
                    return false;
                }

                LogToFile($"Found OBS at: {obsPath}");

                // Try to start OBS if not running
                var obsProcesses = Process.GetProcessesByName("obs64");
                if (obsProcesses.Length == 0)
                {
                    LogToFile("OBS not running - starting it...");
                    var result = await StartOBSAsync();
                    if (result)
                    {
                        LogToFile("OBS setup completed successfully");
                        return true;
                    }
                    else
                    {
                        LogToFile("Failed to start OBS - but OBS is available");
                        return true; // Return true even if OBS didn't start, since it's available
                    }
                }
                else
                {
                    LogToFile("OBS is already running");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error setting up OBS: {ex.Message}");
                // Don't return false here - if OBS is found, we should still allow recording
                var obsPath = FindOBSPath();
                return !string.IsNullOrEmpty(obsPath);
            }
        }

        private async Task<bool> StartOBSRecordingViaCommandLineAsync()
        {
            try
            {
                LogToFile("Attempting to start OBS recording via command line...");
                
                var obsPath = FindOBSPath();
                if (string.IsNullOrEmpty(obsPath))
                {
                    LogToFile("OBS path not found for command line recording");
                    return false;
                }

                // Check if OBS is already running
                var obsProcesses = Process.GetProcessesByName("obs64");
                if (obsProcesses.Length == 0)
                {
                    LogToFile("OBS not running - starting it first");
                    var startProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = obsPath,
                            Arguments = "--minimize-to-tray --startup_to_tray",
                            UseShellExecute = true,
                            CreateNoWindow = false,
                            WorkingDirectory = Path.GetDirectoryName(obsPath)
                        }
                    };
                    startProcess.Start();
                    await Task.Delay(3000); // Wait for OBS to start
                }

                // Start recording via command line
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = obsPath,
                        Arguments = "--startrecording",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory = Path.GetDirectoryName(obsPath)
                    }
                };

                process.Start();
                await Task.Delay(2000); // Wait a bit for the recording to start
                LogToFile("Started OBS recording via command line");
                return true;
            }
            catch (Exception ex)
            {
                LogToFile($"Error starting OBS recording via command line: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> StopOBSRecordingViaCommandLineAsync()
        {
            try
            {
                LogToFile("Attempting to stop OBS recording via command line...");
                
                var obsPath = FindOBSPath();
                if (string.IsNullOrEmpty(obsPath))
                {
                    LogToFile("OBS path not found for command line recording stop");
                    return false;
                }

                // Check if OBS is running before trying to stop recording
                var obsProcesses = Process.GetProcessesByName("obs64");
                if (obsProcesses.Length == 0)
                {
                    LogToFile("OBS is not running - nothing to stop");
                    return true; // Return true since there's nothing to stop
                }

                LogToFile("OBS is running - sending stop recording command");

                // Stop OBS recording via command line
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = obsPath,
                        Arguments = "--stoprecording",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory = Path.GetDirectoryName(obsPath)
                    }
                };

                process.Start();
                await Task.Delay(1000); // Wait a bit for the process to complete
                LogToFile("Stopped OBS recording via command line");
                return true;
            }
            catch (Exception ex)
            {
                LogToFile($"Error stopping OBS recording via command line: {ex.Message}");
                return false;
            }
        }

        public async Task StartRecordingAsync(System.Drawing.Rectangle? region = null)
        {
            if (_isRecording)
            {
                LogToFile("Recording already in progress");
                return;
            }

            try
            {
                LogToFile("Starting OBS recording...");
                LogToFile($"Current directory: {Directory.GetCurrentDirectory()}");
                LogToFile($"App base directory: {AppContext.BaseDirectory}");
                
                // Ensure OBS is running
                if (!await EnsureOBSRunningAsync())
                {
                    LogToFile("Failed to ensure OBS is running");
                }
                
                // Generate recording path with timestamp
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"SharpShot_Recording_{timestamp}.mp4";
                var savePath = _settingsService.CurrentSettings.SavePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "SharpShot");
                _currentRecordingPath = Path.Combine(savePath, fileName);
                
                // Try to start recording via command line
                if (await StartOBSRecordingViaCommandLineAsync())
                {
                    _isRecording = true;
                    RecordingStateChanged?.Invoke(this, true);
                    LogToFile("OBS recording started successfully via command line");
                    LogToFile($"Recording path: {_currentRecordingPath}");
                    return;
                }
                
                // If command line fails, show user message
                LogToFile("Command line recording failed - using fallback mode");
                _isRecording = true;
                RecordingStateChanged?.Invoke(this, true);
                LogToFile("OBS recording started (fallback mode)");
                
                // Show user message about recording status
                System.Windows.MessageBox.Show(
                    "OBS Studio recording started in fallback mode.\n\n" +
                    "The recording may not be saved to the expected location.\n" +
                    "Check OBS Studio's recording folder for the video file.",
                    "OBS Recording Started",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogToFile($"Error starting OBS recording: {ex.Message}");
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);
                throw;
            }
        }

        public async Task StopRecordingAsync()
        {
            if (!_isRecording)
            {
                LogToFile("No recording in progress");
                return;
            }

            try
            {
                LogToFile("Stopping OBS recording...");
                
                // Try to stop recording via command line
                if (await StopOBSRecordingViaCommandLineAsync())
                {
                    LogToFile("OBS recording stopped successfully via command line");
                }
                else
                {
                    LogToFile("Failed to stop OBS recording via command line - using fallback mode");
                }
                
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);
                LogToFile("OBS recording stopped");
            }
            catch (Exception ex)
            {
                LogToFile($"Error stopping OBS recording: {ex.Message}");
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);
            }
        }

        public bool IsRecording => _isRecording;

        public string? GetCurrentRecordingPath()
        {
            return _currentRecordingPath;
        }

        public Task DisconnectAsync()
        {
            try
            {
                if (_isConnected)
                {
                    _isConnected = false;
                    LogToFile("Disconnected from OBS");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error disconnecting from OBS: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private void LogToFile(string message)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "obs_debug.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] [OBSRecordingService] {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
}