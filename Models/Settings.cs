using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SharpShot.Models
{
    public class Settings : INotifyPropertyChanged
    {
        private string _savePath;
        private string _screenshotFormat;
        private string _videoQuality;
        private bool _enableAudioRecording;
        private bool _enableGlobalHotkeys;
        private bool _startMinimized;
        private Dictionary<string, string> _hotkeys;

        public Settings()
        {
            // Default values
            SavePath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\SharpShot";
            ScreenshotFormat = "PNG";
            VideoQuality = "High";
            EnableAudioRecording = true;
            EnableGlobalHotkeys = true;
            StartMinimized = false;
            
            // Default hotkeys
            Hotkeys = new Dictionary<string, string>
            {
                { "RegionCapture", "DoubleCtrl" },
                { "FullScreenCapture", "Ctrl+Shift+S" },
                { "PinScreenshot", "Ctrl+T" },
                { "ToggleRecording", "Ctrl+Shift+R" },
                { "Cancel", "Escape" },
                { "Save", "Space" },
                { "Copy", "Enter" }
            };
        }

        public string SavePath
        {
            get => _savePath;
            set => SetProperty(ref _savePath, value);
        }

        public string ScreenshotFormat
        {
            get => _screenshotFormat;
            set => SetProperty(ref _screenshotFormat, value);
        }

        public string VideoQuality
        {
            get => _videoQuality;
            set => SetProperty(ref _videoQuality, value);
        }

        public bool EnableAudioRecording
        {
            get => _enableAudioRecording;
            set => SetProperty(ref _enableAudioRecording, value);
        }

        public bool EnableGlobalHotkeys
        {
            get => _enableGlobalHotkeys;
            set => SetProperty(ref _enableGlobalHotkeys, value);
        }

        public bool StartMinimized
        {
            get => _startMinimized;
            set => SetProperty(ref _startMinimized, value);
        }

        public Dictionary<string, string> Hotkeys
        {
            get => _hotkeys;
            set => SetProperty(ref _hotkeys, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
} 