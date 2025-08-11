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
                
                // Always refresh our process tracking to handle cases where OBS was closed manually
                RefreshOBSProcessTracking();
                
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
        
        private void RefreshOBSProcessTracking()
        {
            try
            {
                // Check if our tracked process is still valid
                if (_obsProcess != null)
                {
                    try
                    {
                        // Try to access the process - this will throw if the process is no longer running
                        var _ = _obsProcess.ProcessName;
                        if (_obsProcess.HasExited)
                        {
                            _obsProcess = null;
                            _isConnected = false;
                        }
                    }
                    catch
                    {
                        _obsProcess = null;
                        _isConnected = false;
                    }
                }
                
                // If we don't have a valid process reference, try to find one
                if (_obsProcess == null)
                {
                    var obsProcesses = Process.GetProcessesByName("obs64");
                    if (obsProcesses.Length > 0)
                    {
                        _obsProcess = obsProcesses[0];
                        _isConnected = true;
                    }
                    else
                    {
                        _isConnected = false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error refreshing OBS process tracking: {ex.Message}");
                _obsProcess = null;
                _isConnected = false;
            }
        }

        private async Task<bool> StartOBSAsync()
        {
            try
            {
                LogToFile("Starting OBS...");
                
                // Find OBS installation
                var obsPath = FindOBSPath();
                if (string.IsNullOrEmpty(obsPath))
                {
                    LogToFile("OBS not found in common locations");
                    return false;
                }

                LogToFile($"Found OBS at: {obsPath}");

                // Start OBS with GUI visible (no minimize arguments so GUI shows up)
                _obsProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = obsPath,
                        Arguments = "--disable-updater --disable-crash-handler",
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
                var startedObsProcesses = Process.GetProcessesByName("obs64");
                if (startedObsProcesses.Length > 0)
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

        // New method to handle "Save with OBS" scenario - just launch OBS
        public Task<bool> SetupOBSForRecordingAsync()
        {
            try
            {
                LogToFile("Launching OBS...");
                
                // Find OBS installation
                var obsPath = FindOBSPath();
                if (string.IsNullOrEmpty(obsPath))
                {
                    LogToFile("OBS not found");
                    return Task.FromResult(false);
                }

                // Simply start OBS process
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = obsPath,
                        Arguments = "--disable-updater --disable-crash-handler",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory = Path.GetDirectoryName(obsPath)
                    }
                };

                process.Start();
                LogToFile("OBS launched successfully");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                LogToFile($"Error launching OBS: {ex.Message}");
                return Task.FromResult(false);
            }
        }
        
        private async Task ForceKillAllOBSProcessesAsync()
        {
            try
            {
                LogToFile("Gracefully stopping and killing all OBS processes...");
                
                // First try graceful shutdown via command line
                TryGracefulOBSShutdown();
                
                // Wait a bit for graceful shutdown
                await Task.Delay(3000);
                
                // Check if any OBS processes remain after graceful shutdown
                var processNames = new[] { "obs64", "obs32", "obs", "OBS" };
                var allKilledProcesses = new List<int>();
                
                foreach (var processName in processNames)
                {
                    var obsProcesses = Process.GetProcessesByName(processName);
                    
                    if (obsProcesses.Length > 0)
                    {
                        LogToFile($"Found {obsProcesses.Length} {processName} process(es) still running after graceful shutdown, force terminating...");
                        
                        // Kill all processes of this name
                        foreach (var process in obsProcesses)
                        {
                            try
                            {
                                if (!process.HasExited && !allKilledProcesses.Contains(process.Id))
                                {
                                    LogToFile($"Force terminating {processName} process ID: {process.Id}");
                                    process.Kill();
                                    allKilledProcesses.Add(process.Id);
                                    
                                    // Wait for the process to die
                                    await process.WaitForExitAsync();
                                    LogToFile($"Process {process.Id} terminated successfully");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogToFile($"Error killing {processName} process {process.Id}: {ex.Message}");
                                // Try using taskkill as fallback
                                try
                                {
                                    var taskKillProcess = new Process
                                    {
                                        StartInfo = new ProcessStartInfo
                                        {
                                            FileName = "taskkill",
                                            Arguments = $"/F /PID {process.Id}",
                                            UseShellExecute = false,
                                            CreateNoWindow = true
                                        }
                                    };
                                    taskKillProcess.Start();
                                    await taskKillProcess.WaitForExitAsync();
                                    LogToFile($"Force killed process {process.Id} using taskkill");
                                }
                                catch (Exception taskKillEx)
                                {
                                    LogToFile($"Taskkill also failed for process {process.Id}: {taskKillEx.Message}");
                                }
                            }
                            finally
                            {
                                try { process.Dispose(); } catch { }
                            }
                        }
                    }
                }
                
                // Clear our internal tracking
                _obsProcess = null;
                _isConnected = false;
                
                // Wait for system cleanup and verify no processes remain
                await Task.Delay(2000);
                
                // Double-check that all OBS processes are gone
                var remainingProcesses = 0;
                foreach (var processName in processNames)
                {
                    var remaining = Process.GetProcessesByName(processName);
                    remainingProcesses += remaining.Length;
                    if (remaining.Length > 0)
                    {
                        LogToFile($"WARNING: {remaining.Length} {processName} process(es) still running after kill attempt");
                    }
                    foreach (var p in remaining) { try { p.Dispose(); } catch { } }
                }
                
                if (remainingProcesses == 0)
                {
                    LogToFile("All OBS processes terminated successfully - verified clean");
                }
                else
                {
                    LogToFile($"WARNING: {remainingProcesses} OBS processes may still be running");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error during graceful shutdown and force kill of OBS processes: {ex.Message}");
                // Clear our tracking anyway
                _obsProcess = null;
                _isConnected = false;
            }
        }
        
        private void TryGracefulOBSShutdown()
        {
            try
            {
                LogToFile("Attempting graceful OBS shutdown...");
                
                var obsPath = FindOBSPath();
                if (string.IsNullOrEmpty(obsPath))
                {
                    LogToFile("OBS path not found for graceful shutdown");
                    return;
                }

                // Try to send quit command to OBS
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = obsPath,
                        Arguments = "--quit",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory = Path.GetDirectoryName(obsPath)
                    }
                };

                process.Start();
                LogToFile("Sent graceful shutdown command to OBS");
            }
            catch (Exception ex)
            {
                LogToFile($"Error during graceful OBS shutdown: {ex.Message}");
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

                // Since we're called after SetupOBSForRecordingAsync(), OBS should already be running
                // Just verify it's still there and start recording
                var obsProcesses = Process.GetProcessesByName("obs64");
                if (obsProcesses.Length == 0)
                {
                    LogToFile("WARNING: OBS not found when trying to start recording! It may have been killed or crashed.");
                    return false;
                }

                LogToFile($"Found {obsProcesses.Length} OBS process(es), sending start recording command");

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
                
                // Generate recording path with timestamp
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"SharpShot_Recording_{timestamp}.mp4";
                var savePath = _settingsService.CurrentSettings.SavePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "SharpShot");
                _currentRecordingPath = Path.Combine(savePath, fileName);
                
                // Simply try to start recording - OBS should already be running
                if (await StartOBSRecordingViaCommandLineAsync())
                {
                    _isRecording = true;
                    RecordingStateChanged?.Invoke(this, true);
                    LogToFile("OBS recording started successfully");
                }
                else
                {
                    LogToFile("Failed to start OBS recording");
                }
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