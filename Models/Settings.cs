using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;

namespace SharpShot.Models
{
    public class Settings : INotifyPropertyChanged
    {
        private string _savePath = string.Empty;
        private string _screenshotFormat = string.Empty;
        private string _videoQuality = string.Empty;
        private string _audioRecordingMode = string.Empty;
        private string _recordingEngine = string.Empty;
        private string _selectedOutputAudioDevice = string.Empty;
        private string _selectedInputAudioDevice = string.Empty;
        private bool _enableGlobalHotkeys;
        private bool _startMinimized;
        private Dictionary<string, string> _hotkeys = new();
        private string _iconColor = string.Empty;
        private double _hoverOpacity;
        private double _dropShadowOpacity;
        private string _selectedScreen = string.Empty;
        private bool _autoCopyScreenshots;
        private bool _enableMagnifier;
        private double _magnifierZoomLevel;
        private int _magnifierSize = 200; // Size of the magnifier window in pixels (for stationary mode)
        private int _magnifierFollowSize = 200; // Size of the magnifier window in pixels (for follow cursor/auto modes, max 200)
        private string _magnifierMode = "Follow"; // "Follow", "Stationary", "Auto"
        private string _magnifierStationaryMonitor = "Primary Monitor";
        private double _magnifierStationaryX = 100;
        private double _magnifierStationaryY = 100;
        private List<string> _magnifierAutoStationaryMonitors = new List<string>(); // Monitors that should use stationary in Auto mode
        private List<MagnifierBoundaryBox> _magnifierBoundaryBoxes = new List<MagnifierBoundaryBox>(); // Boundary boxes for magnifier detection
        private string _screenshotEditorDisplayMonitor = string.Empty;
        private bool _disableAllPopups;
        private bool _skipEditorAndAutoCopy;
        private bool _enableAutoUpdateCheck = true;
        private string? _updateRepoOwner;
        private string? _updateRepoName;

        public Settings()
        {
            // Default values - automatically detect user's Pictures folder
            SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SharpShot");
            ScreenshotFormat = "PNG";
            VideoQuality = "High";
            AudioRecordingMode = "No Audio";
            RecordingEngine = "FFmpeg";
            SelectedOutputAudioDevice = string.Empty;
            SelectedInputAudioDevice = string.Empty;
            EnableGlobalHotkeys = false;
            StartMinimized = false;
            IconColor = "#FFFF8C00";
            HoverOpacity = 0.125;
            DropShadowOpacity = 0.15;
            SelectedScreen = "Primary Monitor"; // Default to primary monitor
            AutoCopyScreenshots = false; // Default to false - user must manually copy
            EnableMagnifier = true; // Default to true - magnifier helps with precise selection
            MagnifierZoomLevel = 2.0; // Default to 2x zoom
            MagnifierSize = 200; // Default to 200x200 pixels (for stationary mode)
            MagnifierFollowSize = 200; // Default to 200x200 pixels (for follow cursor/auto modes, max 200)
            MagnifierMode = "Follow"; // Default to follow cursor
            MagnifierStationaryMonitor = "Primary Monitor"; // Default to primary monitor for stationary mode
            MagnifierStationaryX = 100; // Default X position for stationary mode
            MagnifierStationaryY = 100; // Default Y position for stationary mode
            MagnifierAutoStationaryMonitors = new List<string>(); // Default: no monitors auto-switch to stationary
            MagnifierBoundaryBoxes = new List<MagnifierBoundaryBox>(); // Default: no boundary boxes
            ScreenshotEditorDisplayMonitor = "Primary Monitor"; // Default to primary monitor for editor display
            DisableAllPopups = false; // Default to false - show popups
            SkipEditorAndAutoCopy = false; // Default to false - show editor
            EnableAutoUpdateCheck = true; // Default to true - check for updates automatically
            UpdateRepoOwner = null; // Will use default from UpdateService
            UpdateRepoName = null; // Will use default from UpdateService
            
            // Start with empty hotkeys - users will set their own
            Hotkeys = new Dictionary<string, string>();
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

        public string AudioRecordingMode
        {
            get => _audioRecordingMode;
            set => SetProperty(ref _audioRecordingMode, value);
        }

        public string RecordingEngine
        {
            get => _recordingEngine;
            set => SetProperty(ref _recordingEngine, value);
        }

        public string SelectedOutputAudioDevice
        {
            get => _selectedOutputAudioDevice;
            set => SetProperty(ref _selectedOutputAudioDevice, value);
        }

        public string SelectedInputAudioDevice
        {
            get => _selectedInputAudioDevice;
            set => SetProperty(ref _selectedInputAudioDevice, value);
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

        public string IconColor
        {
            get => _iconColor;
            set => SetProperty(ref _iconColor, value);
        }

        public double HoverOpacity
        {
            get => _hoverOpacity;
            set => SetProperty(ref _hoverOpacity, value);
        }

        public double DropShadowOpacity
        {
            get => _dropShadowOpacity;
            set => SetProperty(ref _dropShadowOpacity, value);
        }

        public string SelectedScreen
        {
            get => _selectedScreen;
            set => SetProperty(ref _selectedScreen, value);
        }

        public bool AutoCopyScreenshots
        {
            get => _autoCopyScreenshots;
            set => SetProperty(ref _autoCopyScreenshots, value);
        }

        public bool EnableMagnifier
        {
            get => _enableMagnifier;
            set => SetProperty(ref _enableMagnifier, value);
        }

        public double MagnifierZoomLevel
        {
            get => _magnifierZoomLevel;
            set => SetProperty(ref _magnifierZoomLevel, value);
        }

        public int MagnifierSize
        {
            get => _magnifierSize;
            set => SetProperty(ref _magnifierSize, value);
        }

        public int MagnifierFollowSize
        {
            get => _magnifierFollowSize;
            set => SetProperty(ref _magnifierFollowSize, Math.Min(200, value)); // Cap at 200 for follow cursor mode
        }

        public string MagnifierMode
        {
            get => _magnifierMode;
            set => SetProperty(ref _magnifierMode, value);
        }

        public string MagnifierStationaryMonitor
        {
            get => _magnifierStationaryMonitor;
            set => SetProperty(ref _magnifierStationaryMonitor, value);
        }

        public double MagnifierStationaryX
        {
            get => _magnifierStationaryX;
            set => SetProperty(ref _magnifierStationaryX, value);
        }

        public double MagnifierStationaryY
        {
            get => _magnifierStationaryY;
            set => SetProperty(ref _magnifierStationaryY, value);
        }

        public List<string> MagnifierAutoStationaryMonitors
        {
            get => _magnifierAutoStationaryMonitors;
            set => SetProperty(ref _magnifierAutoStationaryMonitors, value);
        }

        public List<MagnifierBoundaryBox> MagnifierBoundaryBoxes
        {
            get => _magnifierBoundaryBoxes;
            set => SetProperty(ref _magnifierBoundaryBoxes, value);
        }

        public string ScreenshotEditorDisplayMonitor
        {
            get => _screenshotEditorDisplayMonitor;
            set => SetProperty(ref _screenshotEditorDisplayMonitor, value);
        }

        public bool DisableAllPopups
        {
            get => _disableAllPopups;
            set => SetProperty(ref _disableAllPopups, value);
        }

        public bool SkipEditorAndAutoCopy
        {
            get => _skipEditorAndAutoCopy;
            set => SetProperty(ref _skipEditorAndAutoCopy, value);
        }

        public bool EnableAutoUpdateCheck
        {
            get => _enableAutoUpdateCheck;
            set => SetProperty(ref _enableAutoUpdateCheck, value);
        }

        public string? UpdateRepoOwner
        {
            get => _updateRepoOwner;
            set => SetProperty(ref _updateRepoOwner, value);
        }

        public string? UpdateRepoName
        {
            get => _updateRepoName;
            set => SetProperty(ref _updateRepoName, value);
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
    
    public class MagnifierBoundaryBox
    {
        public string Name { get; set; } = "";
        public string MonitorId { get; set; } = "";
        public System.Drawing.Rectangle Bounds { get; set; }
        public bool Enabled { get; set; } = true;
    }
} 