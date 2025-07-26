using System;
using System.Windows;
using SharpShot.Services;
using SharpShot.Models;
using SharpShot.Utils;

namespace SharpShot
{
    public partial class App : Application
    {
        private SettingsService _settingsService;
        private HotkeyManager _hotkeyManager;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialize services
            _settingsService = new SettingsService();
            _hotkeyManager = new HotkeyManager(_settingsService);
            
            // Load settings
            _settingsService.LoadSettings();
            
            // Initialize hotkeys
            _hotkeyManager.Initialize();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Save settings and cleanup
            _settingsService?.SaveSettings();
            _hotkeyManager?.Dispose();
            
            base.OnExit(e);
        }
    }
} 