using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SharpShot.Models;
using SharpShot.Services;
// using ScreenRecorderLib; // Temporarily commented out for MSIX build

namespace SharpShot.Services
{
    public class RecordingService
    {
        private readonly SettingsService _settingsService;
        private readonly OBSRecordingService? _obsRecordingService;
        private string? _currentRecordingPath;

        private Process? _ffmpegProcess; // Track FFmpeg process for stopping
        private bool _isRecording = false;

        // Events that MainWindow expects
        public event EventHandler<bool>? RecordingStateChanged;
        public event EventHandler<TimeSpan>? RecordingTimeUpdated;

        public RecordingService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            
            // Initialize OBS recording service
            try
            {
                _obsRecordingService = new OBSRecordingService(settingsService);
                _obsRecordingService.RecordingStateChanged += OnOBSRecordingStateChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize OBS recording service: {ex.Message}");
                _obsRecordingService = null;
            }
        }

        public async Task StartRecordingAsync(System.Drawing.Rectangle? region = null)
        {
            if (_isRecording)
            {
                System.Diagnostics.Debug.WriteLine("Recording already in progress");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"=== StartRecordingAsync called ===");
                System.Diagnostics.Debug.WriteLine($"Region parameter: {(region.HasValue ? region.Value.ToString() : "null")}");
                
                var settings = _settingsService.CurrentSettings;
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"recording_{timestamp}.mp4";
                _currentRecordingPath = Path.Combine(settings.SavePath, fileName);
                
                System.Diagnostics.Debug.WriteLine($"Recording engine: {settings.RecordingEngine}");

                // Choose recording engine based on settings
                switch (settings.RecordingEngine)
                {
                    case "OBS":
                        await StartOBSRecordingAsync(region);
                        break;
                    case "FFmpeg":
                    default:
                        await StartFFmpegRecordingAsync(region);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting recording: {ex.Message}");
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);
            }
        }

        private async Task StartOBSRecordingAsync(System.Drawing.Rectangle? region = null)
        {
            if (_obsRecordingService == null)
            {
                throw new Exception("OBS recording service is not available. Please ensure OBS is installed.");
            }

            try
            {
                LogToFile("Starting OBS recording...");
                await _obsRecordingService.StartRecordingAsync(region);
                
                _isRecording = true;
                RecordingStateChanged?.Invoke(this, true);
                
                LogToFile("OBS recording started successfully");
            }
            catch (Exception ex)
            {
                LogToFile($"Error starting OBS recording: {ex.Message}");
                throw;
            }
        }



        private async Task StartFFmpegRecordingAsync(System.Drawing.Rectangle? region = null)
        {
            var settings = _settingsService.CurrentSettings;
            
            // Build FFmpeg command - pass the region directly and let BuildFFmpegCommand handle the logic
            var ffmpegArgs = await BuildFFmpegCommand(region, settings);
            
            // Start FFmpeg process
            _ffmpegProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = GetFFmpegPath(),
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    RedirectStandardInput = true,  // Enable input redirection for 'q' command
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_currentRecordingPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            _ffmpegProcess.Start();
            
            _isRecording = true;
            RecordingStateChanged?.Invoke(this, true);

            System.Diagnostics.Debug.WriteLine($"Started FFmpeg recording to: {_currentRecordingPath}");
            System.Diagnostics.Debug.WriteLine($"FFmpeg command: {ffmpegArgs}");
            
            // Log stderr output for debugging
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_ffmpegProcess.StandardError.EndOfStream)
                    {
                        var line = await _ffmpegProcess.StandardError.ReadLineAsync();
                        if (!string.IsNullOrEmpty(line))
                        {
                            System.Diagnostics.Debug.WriteLine($"FFmpeg: {line}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading FFmpeg stderr: {ex.Message}");
                }
            });
            
            // Method is async so no explicit return needed
        }

        private async Task<string> BuildFFmpegCommand(System.Drawing.Rectangle? region, Settings settings)
        {
            var inputArgs = "";
            
            // Configure input based on whether we have a specific region or full screen
            if (region.HasValue && region.Value != System.Drawing.Rectangle.Empty)
            {
                // For region recording, capture only the specified region
                var bounds = region.Value;
                
                // Ensure bounds are valid and positive
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid region bounds: {bounds}, falling back to full screen");
                    var screenBounds = GetBoundsForSelectedScreen();
                    inputArgs = $"-f gdigrab -framerate 30 -offset_x {screenBounds.X} -offset_y {screenBounds.Y} -video_size {screenBounds.Width}x{screenBounds.Height} -i desktop -probesize 10M -thread_queue_size 512";
                }
                else
                {
                    // For GDI capture, ensure even dimensions for better compatibility
                    var adjustedWidth = bounds.Width % 2 == 0 ? bounds.Width : bounds.Width - 1;
                    var adjustedHeight = bounds.Height % 2 == 0 ? bounds.Height : bounds.Height - 1;
                    
                    inputArgs = $"-f gdigrab -framerate 30 -offset_x {bounds.X} -offset_y {bounds.Y} -video_size {adjustedWidth}x{adjustedHeight} -i desktop -probesize 10M -thread_queue_size 512";
                    System.Diagnostics.Debug.WriteLine($"Region recording: {bounds.X},{bounds.Y} {adjustedWidth}x{adjustedHeight} (original: {bounds.Width}x{bounds.Height})");
                }
            }
            else
            {
                // For full screen recording, respect the selected monitor setting
                var screenBounds = GetBoundsForSelectedScreen();
                System.Diagnostics.Debug.WriteLine($"Full screen recording on: {settings.SelectedScreen}");
                System.Diagnostics.Debug.WriteLine($"Screen bounds: {screenBounds.X},{screenBounds.Y} {screenBounds.Width}x{screenBounds.Height}");
                
                if (settings.SelectedScreen == "All Screens" || settings.SelectedScreen == "All Monitors")
                {
                    // Capture entire virtual desktop (all monitors)
                    inputArgs = $"-f gdigrab -framerate 30 -i desktop -probesize 10M -thread_queue_size 512";
                    System.Diagnostics.Debug.WriteLine("Recording all screens - no offset/size specified");
                }
                else
                {
                    // Capture specific monitor - offset parameters MUST come before -i desktop
                    inputArgs = $"-f gdigrab -framerate 30 -offset_x {screenBounds.X} -offset_y {screenBounds.Y} -video_size {screenBounds.Width}x{screenBounds.Height} -i desktop -probesize 10M -thread_queue_size 512";
                    System.Diagnostics.Debug.WriteLine($"Recording specific screen with offset: {screenBounds.X},{screenBounds.Y} size: {screenBounds.Width}x{screenBounds.Height}");
                }
            }
            
            // Audio input disabled for now - always record video only to avoid DirectShow issues
            var audioInput = "";
            
            // Log that audio is disabled
            try
            {
                var audioLogPath = Path.Combine(Path.GetTempPath(), "sharpshot_audio_disabled.log");
                await File.WriteAllTextAsync(audioLogPath, $"Audio recording disabled for simplified operation\nOriginal setting: {settings.AudioRecordingMode}\nTime: {DateTime.Now}");
            }
            catch
            {
                // Ignore logging errors - don't let them break recording
            }
            
            // Build output arguments with better encoding settings for maximum compatibility
            var outputArgs = $"-c:v libx264 -preset fast -crf 20 -pix_fmt yuv420p -profile:v baseline -level 3.0";
            
            // Add key frame settings for better playback (framerate already set in input)
            outputArgs += " -g 60 -keyint_min 30";
            
            // Audio codec disabled - recording video only for now
            // This avoids any audio-related FFmpeg issues
            
            // Add proper MP4 formatting and compatibility flags
            outputArgs += " -movflags +faststart -f mp4 -strict experimental";
            
            outputArgs += $" \"{_currentRecordingPath}\"";
            
            return $"{inputArgs}{audioInput} {outputArgs}";
        }

        private System.Drawing.Rectangle GetBoundsForSelectedScreen()
        {
            var selectedScreen = _settingsService.CurrentSettings.SelectedScreen;
            var allScreens = System.Windows.Forms.Screen.AllScreens;
            
            System.Diagnostics.Debug.WriteLine("=== GetBoundsForSelectedScreen Debug ===");
            System.Diagnostics.Debug.WriteLine($"Selected screen from settings: '{selectedScreen}'");
            System.Diagnostics.Debug.WriteLine($"Total screens detected: {allScreens.Length}");
            
            for (int i = 0; i < allScreens.Length; i++)
            {
                var screen = allScreens[i];
                System.Diagnostics.Debug.WriteLine($"Screen {i + 1}: Bounds={screen.Bounds}, Primary={screen.Primary}, DeviceName={screen.DeviceName}");
            }
            
            if (allScreens.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("No screens detected, using fallback");
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    System.Diagnostics.Debug.WriteLine("No primary screen, using default 1920x1080");
                    return new System.Drawing.Rectangle(0, 0, 1920, 1080);
                }
                System.Diagnostics.Debug.WriteLine($"Using primary screen fallback: {primaryScreen.Bounds}");
                return primaryScreen.Bounds;
            }
            
            System.Drawing.Rectangle result;
            
            switch (selectedScreen)
            {
                case "All Screens":
                case "All Monitors": // Backward compatibility
                    System.Diagnostics.Debug.WriteLine("Using All Screens mode");
                    result = GetVirtualDesktopBounds();
                    break;
                    
                case "Primary Monitor":
                    System.Diagnostics.Debug.WriteLine("Using Primary Monitor mode");
                    var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                    if (primaryScreen == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Primary screen is null, using default");
                        result = new System.Drawing.Rectangle(0, 0, 1920, 1080);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Primary screen bounds: {primaryScreen.Bounds}");
                        result = primaryScreen.Bounds;
                    }
                    break;
                    
                default:
                    System.Diagnostics.Debug.WriteLine($"Checking if '{selectedScreen}' is a monitor number");
                    if (selectedScreen.StartsWith("Monitor "))
                    {
                        var monitorNumber = selectedScreen.Replace("Monitor ", "").Replace(" (Primary)", "");
                        System.Diagnostics.Debug.WriteLine($"Extracted monitor number: '{monitorNumber}'");
                        if (int.TryParse(monitorNumber, out int index) && index > 0 && index <= allScreens.Length)
                        {
                            var targetScreen = allScreens[index - 1];
                            System.Diagnostics.Debug.WriteLine($"Using Monitor {index} with bounds: {targetScreen.Bounds}");
                            result = targetScreen.Bounds;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Invalid monitor number, using virtual desktop");
                            result = GetVirtualDesktopBounds();
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Unknown screen selection '{selectedScreen}', using virtual desktop");
                        result = GetVirtualDesktopBounds();
                    }
                    break;
            }
            
            System.Diagnostics.Debug.WriteLine($"Final screen bounds: {result}");
            System.Diagnostics.Debug.WriteLine("=== End GetBoundsForSelectedScreen Debug ===");
            return result;
        }

        private System.Drawing.Rectangle GetVirtualDesktopBounds()
        {
            var allScreens = System.Windows.Forms.Screen.AllScreens;
            if (allScreens.Length == 0)
            {
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    return new System.Drawing.Rectangle(0, 0, 1920, 1080);
                }
                return primaryScreen.Bounds;
            }

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var screen in allScreens)
            {
                minX = Math.Min(minX, screen.Bounds.X);
                minY = Math.Min(minY, screen.Bounds.Y);
                maxX = Math.Max(maxX, screen.Bounds.X + screen.Bounds.Width);
                maxY = Math.Max(maxY, screen.Bounds.Y + screen.Bounds.Height);
            }

            return new System.Drawing.Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        private string GetFFmpegPath()
        {
            // Look for FFmpeg in common locations
            var possiblePaths = new[]
            {
                "ffmpeg.exe",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return "ffmpeg.exe"; // Assume it's in PATH
        }

        public async Task StopRecordingAsync()
        {
            if (!_isRecording)
            {
                System.Diagnostics.Debug.WriteLine("No recording in progress");
                return;
            }

            try
            {
                var settings = _settingsService.CurrentSettings;
                
                switch (settings.RecordingEngine)
                {
                    case "OBS":
                        if (_obsRecordingService != null)
                        {
                            await _obsRecordingService.StopRecordingAsync();
                        }
                        break;
                    case "FFmpeg":
                        // Stop FFmpeg process by sending 'q' command or killing the process
                        if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                        {
                            try
                            {
                                // Try to gracefully stop FFmpeg by sending 'q' command
                                _ffmpegProcess.StandardInput.WriteLine("q");
                                _ffmpegProcess.StandardInput.Flush();
                                _ffmpegProcess.StandardInput.Close(); // Close input to signal end
                                
                                // Wait longer for graceful shutdown and file finalization
                                if (!_ffmpegProcess.WaitForExit(5000))
                                {
                                    // If graceful shutdown failed, force kill
                                    _ffmpegProcess.Kill();
                                }
                                
                                System.Diagnostics.Debug.WriteLine("Stopped FFmpeg recording");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error stopping FFmpeg gracefully, force killing: {ex.Message}");
                                try
                                {
                                    _ffmpegProcess.Kill();
                                }
                                catch (Exception killEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error force killing FFmpeg: {killEx.Message}");
                                }
                            }
                            finally
                            {
                                _ffmpegProcess.Dispose();
                                _ffmpegProcess = null;
                            }
                        }
                        break;
                    default:
                        // Default to FFmpeg stop logic (already handled above)
                        break;
                }
                
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);
                System.Diagnostics.Debug.WriteLine("Recording stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping recording: {ex.Message}");
            }
        }



        // OBS event handlers
        private void OnOBSRecordingStateChanged(object? sender, bool isRecording)
        {
            _isRecording = isRecording;
            RecordingStateChanged?.Invoke(this, isRecording);
        }

        public bool IsRecording => _isRecording;

        // Methods that MainWindow expects
        public async Task StartRecording(System.Drawing.Rectangle? region = null)
        {
            await StartRecordingAsync(region);
        }

        public async Task StopRecording()
        {
            await StopRecordingAsync();
        }

        public string? GetCurrentRecordingPath()
        {
            return _currentRecordingPath;
        }

        // Cleanup method for OBS
        public async Task DisconnectOBSAsync()
        {
            if (_obsRecordingService != null)
            {
                await _obsRecordingService.DisconnectAsync();
            }
        }

        // Method to setup OBS for recording (used by the OBS button)
        public async Task<bool> SetupOBSForRecordingAsync()
        {
            if (_obsRecordingService != null)
            {
                return await _obsRecordingService.SetupOBSForRecordingAsync();
            }
            return false;
        }

        private void LogToFile(string message)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio_debug.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] [RecordingService] {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
} 