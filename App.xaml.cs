using System;
using System.IO;
using System.Windows;
using SharpShot.Services;
using SharpShot.Models;
using SharpShot.Utils;

namespace SharpShot
{
    public partial class App : Application
    {
        private SettingsService _settingsService = null!;
        private HotkeyManager _hotkeyManager = null!;

        // Make SettingsService accessible to other parts of the app
        public static SettingsService SettingsService => ((App)Current)._settingsService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialize services
            _settingsService = new SettingsService();
            _hotkeyManager = new HotkeyManager(_settingsService);
            
            // Load settings
            _settingsService.LoadSettings();
            
            // Ensure default save directory exists
            EnsureDefaultSaveDirectoryExists();
            
            // Debug: Verify settings are loaded
            System.Diagnostics.Debug.WriteLine($"App startup - Settings loaded - IconColor: {_settingsService.CurrentSettings.IconColor}, SavePath: {_settingsService.CurrentSettings.SavePath}");
            
            // Initialize hotkeys
            _hotkeyManager.Initialize();
        }

        private void EnsureDefaultSaveDirectoryExists()
        {
            try
            {
                var savePath = _settingsService.CurrentSettings.SavePath;
                if (!string.IsNullOrEmpty(savePath) && !Directory.Exists(savePath))
                {
                    Directory.CreateDirectory(savePath);
                    System.Diagnostics.Debug.WriteLine($"Created default save directory: {savePath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create default save directory: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Debug: Verify settings before saving
            System.Diagnostics.Debug.WriteLine($"App exit - Settings before save - IconColor: {_settingsService?.CurrentSettings?.IconColor}, SavePath: {_settingsService?.CurrentSettings?.SavePath}");
            
            // Save settings and cleanup
            _settingsService?.SaveSettings();
            _hotkeyManager?.Dispose();
            
            base.OnExit(e);
        }
    }
} 