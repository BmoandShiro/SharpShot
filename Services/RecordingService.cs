using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpShot.Models;

namespace SharpShot.Services
{
    public class RecordingService
    {
        private readonly SettingsService _settingsService;
        private bool _isRecording;
        private DateTime _recordingStartTime;
        private string _currentRecordingPath;

        public RecordingService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _isRecording = false;
        }

        public bool IsRecording => _isRecording;

        public TimeSpan RecordingDuration => _isRecording ? DateTime.Now - _recordingStartTime : TimeSpan.Zero;

        public event Action<bool>? RecordingStateChanged;
        public event Action<TimeSpan>? RecordingTimeUpdated;

        public async Task StartRecording(Rectangle? region = null)
        {
            if (_isRecording) return;

            try
            {
                _isRecording = true;
                _recordingStartTime = DateTime.Now;
                _currentRecordingPath = GenerateRecordingPath();

                // For now, we'll use a simple approach with Media Foundation
                // In a full implementation, you'd use ScreenRecorderLib or similar
                await Task.Run(() =>
                {
                    // Placeholder for actual recording implementation
                    System.Diagnostics.Debug.WriteLine($"Started recording to: {_currentRecordingPath}");
                });

                RecordingStateChanged?.Invoke(true);
                StartRecordingTimer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start recording: {ex.Message}");
                _isRecording = false;
            }
        }

        public async Task StopRecording()
        {
            if (!_isRecording) return;

            try
            {
                _isRecording = false;

                await Task.Run(() =>
                {
                    // Placeholder for actual recording stop implementation
                    System.Diagnostics.Debug.WriteLine($"Stopped recording: {_currentRecordingPath}");
                });

                RecordingStateChanged?.Invoke(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to stop recording: {ex.Message}");
            }
        }

        public async Task ToggleRecording(Rectangle? region = null)
        {
            if (_isRecording)
            {
                await StopRecording();
            }
            else
            {
                await StartRecording(region);
            }
        }

        private string GenerateRecordingPath()
        {
            var quality = _settingsService.CurrentSettings.VideoQuality.ToLower();
            var extension = "mp4";
            var fileName = $"SharpShot_Recording_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
            var savePath = Path.Combine(_settingsService.CurrentSettings.SavePath, fileName);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return savePath;
        }

        private void StartRecordingTimer()
        {
            var timer = new Timer { Interval = 1000 }; // Update every second
            timer.Tick += (sender, e) =>
            {
                if (_isRecording)
                {
                    RecordingTimeUpdated?.Invoke(RecordingDuration);
                }
                else
                {
                    timer.Stop();
                    timer.Dispose();
                }
            };
            timer.Start();
        }

        public string GetCurrentRecordingPath()
        {
            return _currentRecordingPath ?? string.Empty;
        }
    }
} 