using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SharpShot.Services;

namespace SharpShot.Utils
{
    public class HotkeyManager : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly Dictionary<string, Action> _hotkeyActions;
        private bool _isInitialized;

        public HotkeyManager(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _hotkeyActions = new Dictionary<string, Action>();
            _isInitialized = false;
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            _isInitialized = true;

            if (_settingsService.CurrentSettings.EnableGlobalHotkeys)
            {
                RegisterHotkeys();
            }
        }

        public void RegisterHotkeys()
        {
            if (!_isInitialized) return;

            // Setup action mappings
            _hotkeyActions.Clear();
            _hotkeyActions["RegionCapture"] = () => OnRegionCaptureRequested?.Invoke();
            _hotkeyActions["FullScreenCapture"] = () => OnFullScreenCaptureRequested?.Invoke();
            _hotkeyActions["PinScreenshot"] = () => OnPinScreenshotRequested?.Invoke();
            _hotkeyActions["ToggleRecording"] = () => OnToggleRecordingRequested?.Invoke();
            _hotkeyActions["Cancel"] = () => OnCancelRequested?.Invoke();
            _hotkeyActions["Save"] = () => OnSaveRequested?.Invoke();
            _hotkeyActions["Copy"] = () => OnCopyRequested?.Invoke();
        }

        public void UpdateHotkeys()
        {
            if (_isInitialized && _settingsService.CurrentSettings.EnableGlobalHotkeys)
            {
                RegisterHotkeys();
            }
        }

        public void Dispose()
        {
            _isInitialized = false;
        }

        // Events
        public event Action? OnRegionCaptureRequested;
        public event Action? OnFullScreenCaptureRequested;
        public event Action? OnPinScreenshotRequested;
        public event Action? OnToggleRecordingRequested;
        public event Action? OnCancelRequested;
        public event Action? OnSaveRequested;
        public event Action? OnCopyRequested;
    }
} 