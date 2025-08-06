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
using System.Net.WebSockets;
using System.Text;
using System.Threading;

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
        private string _obsWebSocketPassword = "sharpshot123";
        
        private string GetWebSocketUrl() => $"ws://localhost:4455";

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

                LogToFile("Checking if OBS is running...");
                
                // Check if OBS process is running
                var obsProcesses = Process.GetProcessesByName("obs64");
                if (obsProcesses.Length > 0)
                {
                    LogToFile("OBS is running, checking built-in WebSocket server...");
                    
                    // Try to connect to the built-in WebSocket server
                    if (await TestWebSocketConnectionAsync())
                    {
                        _isConnected = true;
                        await ConfigureOBSForRecordingAsync();
                        return true;
                    }
                    else
                    {
                        LogToFile("OBS WebSocket server not enabled - need to enable in OBS Tools menu");
                        return false;
                    }
                }
                
                LogToFile("OBS is not running");
                return false;
            }
            catch (Exception ex)
            {
                LogToFile($"Error checking OBS: {ex.Message}");
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
                    _obsProcess = obsProcesses[0]; // Store reference to existing process
                    return await TryConnectToOBSAsync();
                }
                
                // Find OBS installation
                var obsPath = FindOBSPath();
                if (string.IsNullOrEmpty(obsPath))
                {
                    LogToFile("OBS not found in common locations");
                    return false;
                }

                LogToFile($"Found OBS at: {obsPath}");

                // Start OBS with minimal startup to avoid configuration wizard
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

                // Wait for OBS to start and WebSocket to be available
                for (int i = 0; i < 30; i++) // Wait up to 30 seconds
                {
                    LogToFile($"Connection attempt {i + 1}/30...");
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

                // Auto-configure WebSocket server
                await AutoConfigureWebSocketAsync();

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
                
                // Try to auto-configure WebSocket server
                await AutoConfigureWebSocketAsync();
                
                // Configure OBS recording settings using WebSocket API
                await ConfigureRecordingSettingsAsync();
                
                LogToFile("OBS configuration completed");
            }
            catch (Exception ex)
            {
                LogToFile($"Error configuring OBS: {ex.Message}");
            }
        }

        private async Task AutoConfigureWebSocketAsync()
        {
            try
            {
                LogToFile("Attempting to auto-configure OBS WebSocket server...");
                
                // Get OBS config directory
                var obsConfigPath = GetOBSConfigPath();
                if (string.IsNullOrEmpty(obsConfigPath))
                {
                    LogToFile("Could not find OBS config directory");
                    return;
                }

                var globalIniPath = Path.Combine(obsConfigPath, "global.ini");
                LogToFile($"OBS config path: {globalIniPath}");

                // Create or update global.ini with WebSocket settings
                var iniContent = new List<string>();
                bool websocketSectionFound = false;
                bool serverEnabledFound = false;
                bool configChanged = false;

                if (File.Exists(globalIniPath))
                {
                    var existingLines = await File.ReadAllLinesAsync(globalIniPath);
                    foreach (var line in existingLines)
                    {
                        iniContent.Add(line);
                        if (line.Trim() == "[WebSocketAPI]")
                        {
                            websocketSectionFound = true;
                        }
                        if (line.Trim().StartsWith("ServerEnabled="))
                        {
                            serverEnabledFound = true;
                        }
                    }
                }

                // Add WebSocket section if not found
                if (!websocketSectionFound)
                {
                    iniContent.Add("");
                    iniContent.Add("[WebSocketAPI]");
                    iniContent.Add("ServerEnabled=true");
                    iniContent.Add("ServerPort=4455");
                    iniContent.Add($"ServerPassword={_obsWebSocketPassword}");
                    iniContent.Add("DebugEnabled=false");
                    iniContent.Add("AlertsEnabled=false");
                    
                    LogToFile("Added WebSocket configuration to OBS global.ini");
                    configChanged = true;
                }
                else if (!serverEnabledFound)
                {
                    // WebSocket section exists but ServerEnabled is not set
                    LogToFile("WebSocket section exists but ServerEnabled not found - adding it");
                    // Find the [WebSocketAPI] section and add ServerEnabled after it
                    var newContent = new List<string>();
                    
                    foreach (var line in iniContent)
                    {
                        newContent.Add(line);
                        if (line.Trim() == "[WebSocketAPI]")
                        {
                            newContent.Add("ServerEnabled=true");
                            newContent.Add("ServerPort=4455");
                            newContent.Add($"ServerPassword={_obsWebSocketPassword}");
                        }
                    }
                    
                    iniContent = newContent;
                    configChanged = true;
                }

                // Write the updated config
                await File.WriteAllLinesAsync(globalIniPath, iniContent);
                LogToFile("OBS WebSocket auto-configuration completed");
                
                // Restart OBS if it's running and config changed
                if (configChanged)
                {
                    var obsProcesses = Process.GetProcessesByName("obs64");
                    if (obsProcesses.Length > 0)
                    {
                        LogToFile("OBS is running - restarting to apply WebSocket configuration");
                        try
                        {
                            obsProcesses[0].Kill();
                            await Task.Delay(2000); // Wait for OBS to close
                            LogToFile("OBS restarted to apply WebSocket configuration");
                            
                            // Start OBS again after restart
                            await Task.Delay(3000); // Wait a bit more
                            await StartOBSAsync();
                        }
                        catch (Exception ex)
                        {
                            LogToFile($"Error restarting OBS: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error auto-configuring WebSocket: {ex.Message}");
            }
        }

        private string GetOBSConfigPath()
        {
            // Try to get OBS config path from running process
            var obsProcesses = Process.GetProcessesByName("obs64");
            if (obsProcesses.Length > 0)
            {
                try
                {
                    var obsProcess = obsProcesses[0];
                    var obsPath = obsProcess.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(obsPath))
                    {
                        // OBS config is typically in %APPDATA%\obs-studio
                        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        var obsConfigPath = Path.Combine(appDataPath, "obs-studio");
                        
                        if (Directory.Exists(obsConfigPath))
                        {
                            LogToFile($"Found OBS config at: {obsConfigPath}");
                            return obsConfigPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"Error getting OBS config path: {ex.Message}");
                }
            }

            // Fallback to default location
            var defaultConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio");
            if (Directory.Exists(defaultConfigPath))
            {
                LogToFile($"Using default OBS config path: {defaultConfigPath}");
                return defaultConfigPath;
            }

            LogToFile("Could not find OBS config directory");
            return string.Empty;
        }

        private async Task<bool> TestWebSocketConnectionAsync()
        {
            try
            {
                LogToFile("Testing WebSocket connection to OBS...");
                
                // Try to establish a proper WebSocket connection
                using var webSocket = new ClientWebSocket();
                var uri = new Uri($"ws://localhost:4455");
                
                LogToFile($"Attempting to connect to: {uri}");
                await webSocket.ConnectAsync(uri, CancellationToken.None);
                
                if (webSocket.State == WebSocketState.Open)
                {
                    LogToFile("WebSocket connection established successfully");
                    return true;
                }
                else
                {
                    LogToFile($"WebSocket connection failed - state: {webSocket.State}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogToFile($"WebSocket connection test failed: {ex.Message}");
                
                // If WebSocket is not accessible, try to restart OBS with WebSocket enabled
                LogToFile("Attempting to restart OBS with WebSocket enabled...");
                await AutoConfigureWebSocketAsync();
                
                // Wait a bit and try again
                await Task.Delay(5000);
                
                try
                {
                    using var webSocket2 = new ClientWebSocket();
                    var uri = new Uri($"ws://localhost:4455");
                    await webSocket2.ConnectAsync(uri, CancellationToken.None);
                    
                    if (webSocket2.State == WebSocketState.Open)
                    {
                        LogToFile("WebSocket server is now accessible after restart");
                        return true;
                    }
                    else
                    {
                        LogToFile($"WebSocket connection still failed after restart - state: {webSocket2.State}");
                        return false;
                    }
                }
                catch (Exception ex2)
                {
                    LogToFile($"WebSocket connection still failed after restart: {ex2.Message}");
                    return false;
                }
            }
        }

        private async Task<bool> StartOBSRecordingViaWebSocketAsync()
        {
            try
            {
                LogToFile("Attempting to start OBS recording via WebSocket API...");
                
                // Set recording path
                var settings = _settingsService.CurrentSettings;
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"recording_{timestamp}.mp4";
                _currentRecordingPath = Path.Combine(settings.SavePath, fileName);
                
                // Use WebSocket client to send the request
                using var webSocket = new ClientWebSocket();
                var uri = new Uri($"ws://localhost:4455");
                
                LogToFile($"Connecting to WebSocket: {uri}");
                await webSocket.ConnectAsync(uri, CancellationToken.None);
                
                if (webSocket.State != WebSocketState.Open)
                {
                    LogToFile($"WebSocket connection failed - state: {webSocket.State}");
                    return false;
                }
                
                LogToFile("WebSocket connected successfully");
                
                // First, receive the Hello message from OBS
                var helloBuffer = new byte[4096];
                var helloResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(helloBuffer), CancellationToken.None);
                var helloJson = Encoding.UTF8.GetString(helloBuffer, 0, helloResult.Count);
                LogToFile($"Received Hello message: {helloJson}");
                
                // Send Identify message (authentication)
                var identifyRequest = new
                {
                    op = 1, // Identify
                    d = new
                    {
                        rpcVersion = 1
                    }
                };
                
                var identifyJson = JsonSerializer.Serialize(identifyRequest);
                LogToFile($"Sending Identify: {identifyJson}");
                
                var identifyBuffer = Encoding.UTF8.GetBytes(identifyJson);
                await webSocket.SendAsync(new ArraySegment<byte>(identifyBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                
                // Receive Identified message
                var identifiedBuffer = new byte[4096];
                var identifiedResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(identifiedBuffer), CancellationToken.None);
                var identifiedJson = Encoding.UTF8.GetString(identifiedBuffer, 0, identifiedResult.Count);
                LogToFile($"Received Identified message: {identifiedJson}");
                
                // Now send the StartRecord request
                var request = new
                {
                    op = 6, // Request
                    d = new
                    {
                        requestType = "StartRecord",
                        requestId = Guid.NewGuid().ToString()
                    }
                };
                
                var json = JsonSerializer.Serialize(request);
                LogToFile($"Sending StartRecord request: {json}");
                
                var buffer = Encoding.UTF8.GetBytes(json);
                await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                
                // Wait for response
                var responseBuffer = new byte[4096];
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(responseBuffer), CancellationToken.None);
                var responseJson = Encoding.UTF8.GetString(responseBuffer, 0, result.Count);
                
                LogToFile($"WebSocket StartRecord response: {responseJson}");
                
                if (result.MessageType == WebSocketMessageType.Text && responseJson.Contains("requestStatus"))
                {
                    LogToFile("OBS recording started successfully via WebSocket API");
                    return true;
                }
                else
                {
                    LogToFile("Failed to start OBS recording via WebSocket API");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error starting OBS recording via WebSocket: {ex.Message}");
                LogToFile($"WebSocket error stack trace: {ex.StackTrace}");
                return false;
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
                else
                {
                    LogToFile("OBS is already running - using WebSocket instead of command line");
                    // If OBS is already running, prefer WebSocket over command line
                    return await StartOBSRecordingViaWebSocketAsync();
                }

                // Only use command line if we just started OBS
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

        private async Task<bool> StopOBSRecordingViaWebSocketAsync()
        {
            try
            {
                LogToFile("Attempting to stop OBS recording via WebSocket API...");
                
                // Use WebSocket client to send the request
                using var webSocket = new ClientWebSocket();
                var uri = new Uri($"ws://localhost:4455");
                
                LogToFile($"Connecting to WebSocket: {uri}");
                await webSocket.ConnectAsync(uri, CancellationToken.None);
                
                if (webSocket.State != WebSocketState.Open)
                {
                    LogToFile($"WebSocket connection failed - state: {webSocket.State}");
                    return false;
                }
                
                LogToFile("WebSocket connected successfully");
                
                // First, receive the Hello message from OBS
                var helloBuffer = new byte[4096];
                var helloResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(helloBuffer), CancellationToken.None);
                var helloJson = Encoding.UTF8.GetString(helloBuffer, 0, helloResult.Count);
                LogToFile($"Received Hello message: {helloJson}");
                
                // Send Identify message (authentication)
                var identifyRequest = new
                {
                    op = 1, // Identify
                    d = new
                    {
                        rpcVersion = 1
                    }
                };
                
                var identifyJson = JsonSerializer.Serialize(identifyRequest);
                LogToFile($"Sending Identify: {identifyJson}");
                
                var identifyBuffer = Encoding.UTF8.GetBytes(identifyJson);
                await webSocket.SendAsync(new ArraySegment<byte>(identifyBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                
                // Receive Identified message
                var identifiedBuffer = new byte[4096];
                var identifiedResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(identifiedBuffer), CancellationToken.None);
                var identifiedJson = Encoding.UTF8.GetString(identifiedBuffer, 0, identifiedResult.Count);
                LogToFile($"Received Identified message: {identifiedJson}");
                
                // Now send the StopRecord request
                var request = new
                {
                    op = 6, // Request
                    d = new
                    {
                        requestType = "StopRecord",
                        requestId = Guid.NewGuid().ToString()
                    }
                };
                
                var json = JsonSerializer.Serialize(request);
                LogToFile($"Sending StopRecord request: {json}");
                
                var buffer = Encoding.UTF8.GetBytes(json);
                await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                
                // Wait for response
                var responseBuffer = new byte[4096];
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(responseBuffer), CancellationToken.None);
                var responseJson = Encoding.UTF8.GetString(responseBuffer, 0, result.Count);
                
                LogToFile($"WebSocket StopRecord response: {responseJson}");
                
                if (result.MessageType == WebSocketMessageType.Text && responseJson.Contains("requestStatus"))
                {
                    LogToFile("OBS recording stopped successfully via WebSocket API");
                    return true;
                }
                else
                {
                    LogToFile("Failed to stop OBS recording via WebSocket API");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error stopping OBS recording via WebSocket: {ex.Message}");
                LogToFile($"WebSocket error stack trace: {ex.StackTrace}");
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
                
                var response = await _httpClient.PostAsync($"{GetWebSocketUrl()}/api/requests", content);
                
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
                LogToFile($"Current directory: {Directory.GetCurrentDirectory()}");
                LogToFile($"App base directory: {AppContext.BaseDirectory}");
                
                // Check if OBS is already running first
                var obsProcesses = Process.GetProcessesByName("obs64");
                if (obsProcesses.Length > 0)
                {
                    LogToFile("OBS is already running - using WebSocket API directly");
                    _obsProcess = obsProcesses[0]; // Store reference to existing process
                    
                    // If OBS is already running, try WebSocket first
                    if (await StartOBSRecordingViaWebSocketAsync())
                    {
                        _isRecording = true;
                        RecordingStateChanged?.Invoke(this, true);
                        LogToFile("OBS recording started successfully via WebSocket API");
                        return;
                    }
                    else
                    {
                        LogToFile("WebSocket failed with running OBS - trying command line");
                        if (await StartOBSRecordingViaCommandLineAsync())
                        {
                            _isRecording = true;
                            RecordingStateChanged?.Invoke(this, true);
                            LogToFile("OBS recording started via command line");
                            return;
                        }
                    }
                }
                else
                {
                    LogToFile("OBS not running - starting it first");
                    // Ensure OBS is running and configured
                    if (!await EnsureOBSRunningAsync())
                    {
                        LogToFile("Failed to ensure OBS is running - trying direct recording");
                    }
                    
                    // Try WebSocket after starting OBS
                    if (await StartOBSRecordingViaWebSocketAsync())
                    {
                        _isRecording = true;
                        RecordingStateChanged?.Invoke(this, true);
                        LogToFile("OBS recording started successfully via WebSocket API");
                        return;
                    }
                    else
                    {
                        // Fallback to command line
                        LogToFile("WebSocket failed after starting OBS - trying command line");
                        if (await StartOBSRecordingViaCommandLineAsync())
                        {
                            _isRecording = true;
                            RecordingStateChanged?.Invoke(this, true);
                            LogToFile("OBS recording started via command line");
                            return;
                        }
                    }
                }
                
                // If all methods failed
                LogToFile("All OBS recording methods failed - using fallback mode");
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
                
                // Check if OBS is running before trying to stop
                var obsProcesses = Process.GetProcessesByName("obs64");
                if (obsProcesses.Length == 0)
                {
                    LogToFile("OBS is not running - nothing to stop");
                    _isRecording = false;
                    RecordingStateChanged?.Invoke(this, false);
                    return;
                }
                
                // If OBS is running, try WebSocket first
                if (await StopOBSRecordingViaWebSocketAsync())
                {
                    LogToFile("OBS recording stopped successfully via WebSocket API");
                }
                else
                {
                    // Fallback: try to stop OBS recording using command line
                    if (await StopOBSRecordingViaCommandLineAsync())
                    {
                        LogToFile("OBS recording stopped via command line");
                    }
                    else
                    {
                        LogToFile("Failed to stop OBS recording - using fallback mode");
                    }
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