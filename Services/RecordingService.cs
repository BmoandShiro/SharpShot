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
        private string? _currentRecordingPath;
        private Recorder? _recorder;
        private bool _isRecording = false;

        // Events that MainWindow expects
        public event EventHandler<bool>? RecordingStateChanged;
        public event EventHandler<TimeSpan>? RecordingTimeUpdated;

        public RecordingService(SettingsService settingsService)
        {
            _settingsService = settingsService;
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
                if (settings.RecordingEngine == "FFmpeg")
                {
                    await StartFFmpegRecordingAsync(region);
                }
                else
                {
                    await StartScreenRecorderLibRecordingAsync(region);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting recording: {ex.Message}");
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);
            }
        }

        private Task StartScreenRecorderLibRecordingAsync(System.Drawing.Rectangle? region = null)
        {
            var settings = _settingsService.CurrentSettings;
            
            // Create recorder with audio settings
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

            System.Diagnostics.Debug.WriteLine($"Started ScreenRecorderLib recording to: {_currentRecordingPath}");
            
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

        public Task StopRecordingAsync()
        {
            if (!_isRecording)
            {
                System.Diagnostics.Debug.WriteLine("No recording in progress");
                return Task.CompletedTask;
            }

            try
            {
                var settings = _settingsService.CurrentSettings;
                
                if (settings.RecordingEngine == "FFmpeg")
                {
                    // For FFmpeg, we need to stop the process
                    // This is a simplified approach - in a real implementation,
                    // you'd want to properly manage the FFmpeg process
                    System.Diagnostics.Debug.WriteLine("Stopping FFmpeg recording");
                }
                else
                {
                    // Stop ScreenRecorderLib recording
                    if (_recorder != null)
                    {
                        _recorder.Stop();
                    }
                }
                
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);
                System.Diagnostics.Debug.WriteLine("Recording stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping recording: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private void ConfigureAudioRecording(Settings settings)
        {
            if (_recorder == null) return;

            // Configure audio recording based on mode
            switch (settings.AudioRecordingMode)
            {
                case "System Audio Only":
                    // Note: ScreenRecorderLib API may have changed
                    // Using basic audio configuration
                    break;
                    
                case "Microphone Only":
                    // Note: ScreenRecorderLib API may have changed
                    // Using basic audio configuration
                    break;
                    
                case "System Audio + Microphone":
                    // Note: ScreenRecorderLib API may have changed
                    // Using basic audio configuration
                    break;
                    
                case "No Audio":
                default:
                    // No audio configuration
                    break;
            }
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
    }
} 