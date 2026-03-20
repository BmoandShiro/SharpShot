using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using SharpShot.Services;
using SharpShot.Models;
using SharpShot.Utils;
using SharpShot.UI;

namespace SharpShot
{
    public partial class App : Application
    {
        private SettingsService _settingsService = null!;
        private HotkeyManager _hotkeyManager = null!;
        private UpdateService? _updateService;

        // Make SettingsService accessible to other parts of the app
        public static SettingsService SettingsService => ((App)Current)._settingsService;
        public static UpdateService? UpdateService => ((App)Current)._updateService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialize services
            _settingsService = new SettingsService();
            _hotkeyManager = new HotkeyManager(_settingsService);
            _updateService = new UpdateService(_settingsService);
            
            // Load settings
            _settingsService.LoadSettings();
            
            // Ensure default save directory exists
            EnsureDefaultSaveDirectoryExists();
            
            // Debug: Verify settings are loaded
            System.Diagnostics.Debug.WriteLine($"App startup - Settings loaded - IconColor: {_settingsService.CurrentSettings.IconColor}, SavePath: {_settingsService.CurrentSettings.SavePath}");
            
            // Initialize hotkeys
            _hotkeyManager.Initialize();

            // Check for updates in background (if enabled)
            if (_settingsService.CurrentSettings.EnableAutoUpdateCheck)
            {
                Task.Run(async () => await CheckForUpdatesAsync());
            }
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

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                if (_updateService == null) return;

                var updateInfo = await _updateService.CheckForUpdatesAsync();
                if (updateInfo != null)
                {
                    // Show update window on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        var updateWindow = new UpdateWindow(_updateService, updateInfo);
                        updateWindow.Show();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking for updates: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Debug: Verify settings before saving
            System.Diagnostics.Debug.WriteLine($"App exit - Settings before save - IconColor: {_settingsService?.CurrentSettings?.IconColor}, SavePath: {_settingsService?.CurrentSettings?.SavePath}");
            
            // Save settings and cleanup
            _settingsService?.SaveSettings();
            _hotkeyManager?.Dispose();
            _updateService?.Dispose();
            
            base.OnExit(e);
        }
    }
} 