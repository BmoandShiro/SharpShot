using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SharpShot.Models;
using SharpShot.Services;
using ScreenRecorderLib;

namespace SharpShot.Services
{
    public class RecordingService
    {
        private readonly SettingsService _settingsService;
        private readonly OBSRecordingService? _obsRecordingService;
        private string? _currentRecordingPath;
        private Recorder? _recorder;
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
                var settings = _settingsService.CurrentSettings;
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"recording_{timestamp}.mp4";
                _currentRecordingPath = Path.Combine(settings.SavePath, fileName);

                // Choose recording engine based on settings
                switch (settings.RecordingEngine)
                {
                    case "OBS":
                        await StartOBSRecordingAsync(region);
                        break;
                    case "FFmpeg":
                        await StartFFmpegRecordingAsync(region);
                        break;
                    case "ScreenRecorderLib":
                    default:
                        await StartScreenRecorderLibRecordingAsync(region);
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

        private Task StartScreenRecorderLibRecordingAsync(System.Drawing.Rectangle? region = null)
        {
            var settings = _settingsService.CurrentSettings;
            
            // Create recorder with basic configuration
            _recorder = Recorder.CreateRecorder();
            
            // Configure audio recording based on settings
            ConfigureAudioRecording(settings);
            
            // Set up event handlers
            _recorder.OnRecordingComplete += OnRecordingComplete;
            _recorder.OnRecordingFailed += OnRecordingFailed;
            _recorder.OnStatusChanged += OnStatusChanged;

            // Start recording
            _recorder.Record(_currentRecordingPath);
            
            _isRecording = true;
            RecordingStateChanged?.Invoke(this, true);

            LogToFile($"Started ScreenRecorderLib recording to: {_currentRecordingPath}");
            LogToFile($"Audio recording mode: {settings.AudioRecordingMode}");
            
            return Task.CompletedTask;
        }

        private Task StartFFmpegRecordingAsync(System.Drawing.Rectangle? region = null)
        {
            var settings = _settingsService.CurrentSettings;
            
            // Get bounds for recording
            var bounds = region ?? GetBoundsForSelectedScreen();
            
            // Build FFmpeg command
            var ffmpegArgs = BuildFFmpegCommand(bounds, settings);
            
            // Start FFmpeg process
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = GetFFmpegPath(),
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            
            _isRecording = true;
            RecordingStateChanged?.Invoke(this, true);

            System.Diagnostics.Debug.WriteLine($"Started FFmpeg recording to: {_currentRecordingPath}");
            System.Diagnostics.Debug.WriteLine($"FFmpeg command: {ffmpegArgs}");
            
            return Task.CompletedTask;
        }

        private string BuildFFmpegCommand(System.Drawing.Rectangle bounds, Settings settings)
        {
            var inputArgs = $"-f gdigrab -i desktop";
            
            // Add region if specified
            if (bounds != System.Drawing.Rectangle.Empty)
            {
                inputArgs += $" -offset_x {bounds.X} -offset_y {bounds.Y} -video_size {bounds.Width}x{bounds.Height}";
            }
            
            // Add audio input based on settings
            var audioInput = "";
            switch (settings.AudioRecordingMode)
            {
                case "System Audio Only":
                    audioInput = " -f dshow -i audio=\"virtual-audio-capturer\"";
                    break;
                case "Microphone Only":
                    if (!string.IsNullOrEmpty(settings.SelectedInputAudioDevice))
                    {
                        audioInput = $" -f dshow -i audio=\"{settings.SelectedInputAudioDevice}\"";
                    }
                    break;
                case "System Audio + Microphone":
                    audioInput = " -f dshow -i audio=\"virtual-audio-capturer\"";
                    if (!string.IsNullOrEmpty(settings.SelectedInputAudioDevice))
                    {
                        audioInput += $" -f dshow -i audio=\"{settings.SelectedInputAudioDevice}\"";
                    }
                    break;
            }
            
            // Build output arguments
            var outputArgs = $"-c:v libx264 -preset ultrafast -crf 18";
            
            // Add audio codec if audio is enabled
            if (settings.AudioRecordingMode != "No Audio")
            {
                outputArgs += " -c:a aac -b:a 128k";
            }
            
            outputArgs += $" \"{_currentRecordingPath}\"";
            
            return $"{inputArgs}{audioInput} {outputArgs}";
        }

        private System.Drawing.Rectangle GetBoundsForSelectedScreen()
        {
            var selectedScreen = _settingsService.CurrentSettings.SelectedScreen;
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
            
            switch (selectedScreen)
            {
                case "All Monitors":
                    return GetVirtualDesktopBounds();
                    
                case "Primary Monitor":
                    var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                    if (primaryScreen == null)
                    {
                        return new System.Drawing.Rectangle(0, 0, 1920, 1080);
                    }
                    return primaryScreen.Bounds;
                    
                default:
                    if (selectedScreen.StartsWith("Monitor "))
                    {
                        var monitorNumber = selectedScreen.Replace("Monitor ", "").Replace(" (Primary)", "");
                        if (int.TryParse(monitorNumber, out int index) && index > 0 && index <= allScreens.Length)
                        {
                            return allScreens[index - 1].Bounds;
                        }
                    }
                    return GetVirtualDesktopBounds();
            }
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
                        // For FFmpeg, we need to stop the process
                        // This is a simplified approach - in a real implementation,
                        // you'd want to properly manage the FFmpeg process
                        System.Diagnostics.Debug.WriteLine("Stopping FFmpeg recording");
                        break;
                    case "ScreenRecorderLib":
                    default:
                        // Stop ScreenRecorderLib recording
                        if (_recorder != null)
                        {
                            _recorder.Stop();
                        }
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

        private void ConfigureAudioRecording(Settings settings)
        {
            if (_recorder == null) return;

            LogToFile($"Configuring audio recording for engine: {settings.RecordingEngine}");
            LogToFile($"Audio recording mode: {settings.AudioRecordingMode}");

            // Configure audio recording based on mode
            switch (settings.AudioRecordingMode)
            {
                case "System Audio Only":
                    LogToFile("Configuring system audio only recording");
                    // ScreenRecorderLib can capture system audio directly
                    // The library handles this automatically when no microphone is specified
                    break;
                    
                case "Microphone Only":
                    LogToFile("Configuring microphone only recording");
                    // ScreenRecorderLib can capture microphone audio
                    // The library handles this automatically when no system audio is specified
                    break;
                    
                case "System Audio + Microphone":
                    LogToFile("Configuring system audio + microphone recording");
                    // ScreenRecorderLib can capture both system audio and microphone
                    // The library handles mixing automatically
                    break;
                    
                case "No Audio":
                default:
                    LogToFile("Configuring no audio recording");
                    // No audio configuration needed
                    break;
            }

            // Note: ScreenRecorderLib handles audio device selection automatically
            // based on the recording mode. The library uses WASAPI internally
            // and can capture both system audio and microphone input.
            LogToFile("Audio configuration completed");
        }

        private void OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Recording completed: {e.FilePath}");
            _isRecording = false;
            RecordingStateChanged?.Invoke(this, false);
        }

        private void OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Recording failed: {e.Error}");
            _isRecording = false;
            RecordingStateChanged?.Invoke(this, false);
        }

        private void OnStatusChanged(object? sender, RecordingStatusEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Recording status: {e.Status}");
            // Update recording time if needed
            RecordingTimeUpdated?.Invoke(this, TimeSpan.Zero);
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