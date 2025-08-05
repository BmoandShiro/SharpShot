using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SharpShot.Models;
using SharpShot.Services;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

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
        private string _obsWebSocketUrl = "http://localhost:4444";

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
                
                // Try to connect to existing OBS instance
                if (await TryConnectToOBSAsync())
                {
                    LogToFile("Successfully connected to existing OBS instance");
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

        private async Task<bool> TryConnectToOBSAsync()
        {
            try
            {
                if (_isConnected)
                {
                    return true;
                }

                LogToFile("Attempting to connect to OBS WebSocket...");
                
                // Try to get OBS version to test connection
                var response = await _httpClient.GetAsync($"{_obsWebSocketUrl}/api/version");
                
                if (response.IsSuccessStatusCode)
                {
                    _isConnected = true;
                    LogToFile("Successfully connected to OBS WebSocket");
                    
                    // Configure OBS for recording
                    await ConfigureOBSForRecordingAsync();
                    return true;
                }
                
                LogToFile("Failed to connect to OBS WebSocket");
                return false;
            }
            catch (Exception ex)
            {
                LogToFile($"Error connecting to OBS: {ex.Message}");
                return false;
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

                // Start OBS with WebSocket server enabled
                _obsProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = obsPath,
                        Arguments = "--minimize-to-tray --websocket_port 4444",
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                };

                _obsProcess.Start();
                LogToFile($"Started OBS process: {obsPath}");

                // Wait for OBS to start and WebSocket to be available
                for (int i = 0; i < 30; i++) // Wait up to 30 seconds
                {
                    await Task.Delay(1000);
                    if (await TryConnectToOBSAsync())
                    {
                        LogToFile("Successfully connected to OBS after startup");
                        return true;
                    }
                }

                LogToFile("Failed to connect to OBS after startup");
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
            var possiblePaths = new[]
            {
                "obs64.exe",
                "obs32.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "64bit", "obs64.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "32bit", "obs32.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "64bit", "obs64.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "32bit", "obs32.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    LogToFile($"Found OBS at: {path}");
                    return path;
                }
            }

            LogToFile("OBS not found in any location");
            return string.Empty;
        }

        private async Task ConfigureOBSForRecordingAsync()
        {
            try
            {
                LogToFile("Configuring OBS for recording...");
                
                var settings = _settingsService.CurrentSettings;
                
                // Set recording path
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"recording_{timestamp}.mp4";
                _currentRecordingPath = Path.Combine(settings.SavePath, fileName);
                
                // Configure OBS recording settings using WebSocket API
                await ConfigureRecordingSettingsAsync();
                
                LogToFile("OBS configuration completed");
            }
            catch (Exception ex)
            {
                LogToFile($"Error configuring OBS: {ex.Message}");
            }
        }

        private async Task ConfigureRecordingSettingsAsync()
        {
            try
            {
                LogToFile("Configuring OBS recording settings...");
                
                // Set recording path using OBS WebSocket API
                var recordingPath = Path.GetDirectoryName(_currentRecordingPath);
                
                // Create request to set recording path
                var request = new
                {
                    requestType = "SetRecordingFolder",
                    recordingFolder = recordingPath
                };
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_obsWebSocketUrl}/api/requests", content);
                
                if (response.IsSuccessStatusCode)
                {
                    LogToFile($"Set recording path to: {recordingPath}");
                }
                else
                {
                    LogToFile($"Failed to set recording path: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error configuring recording settings: {ex.Message}");
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
                
                // Ensure OBS is running and configured
                if (!await EnsureOBSRunningAsync())
                {
                    throw new Exception("Failed to start or connect to OBS");
                }

                // Start recording using OBS WebSocket API
                var request = new
                {
                    requestType = "StartRecording"
                };
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_obsWebSocketUrl}/api/requests", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _isRecording = true;
                    RecordingStateChanged?.Invoke(this, true);
                    LogToFile("OBS recording started successfully");
                }
                else
                {
                    throw new Exception($"Failed to start OBS recording: {response.StatusCode}");
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
                
                // Stop recording using OBS WebSocket API
                var request = new
                {
                    requestType = "StopRecording"
                };
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_obsWebSocketUrl}/api/requests", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _isRecording = false;
                    RecordingStateChanged?.Invoke(this, false);
                    LogToFile("OBS recording stopped successfully");
                }
                else
                {
                    LogToFile($"Failed to stop OBS recording: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error stopping OBS recording: {ex.Message}");
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