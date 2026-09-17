using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
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
        private static Mutex? _instanceMutex;
        private SettingsService _settingsService = null!;
        private HotkeyManager _hotkeyManager = null!;
        private UpdateService? _updateService;

        // Make SettingsService accessible to other parts of the app
        public static SettingsService SettingsService => ((App)Current)._settingsService;
        public static UpdateService? UpdateService => ((App)Current)._updateService;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (!TryBecomeOnlyInstance())
            {
                ActivateExistingInstance();
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Shutdown();
                return;
            }

            // Before any window: group this process with Start Menu / pinned shortcuts.
            PinnedTaskbarIconService.SetProcessAppUserModelId();
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnAnyWindowLoaded));

            base.OnStartup(e);
            
            // Initialize services
            _settingsService = new SettingsService();
            _hotkeyManager = new HotkeyManager(_settingsService);
            // Steam and Microsoft Store own updates. GitHub builds still construct the updater.
            if (!BuildInfo.DisableInAppUpdates)
                _updateService = new UpdateService(_settingsService);
            
            // Load settings
            _settingsService.LoadSettings();
            LocalizationService.LanguageChanged += UiLocalizer.ApplyOpenWindows;
            LocalizationService.ApplySavedLanguage();
            UiLocalizer.ApplyOpenWindows();
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((_, args) =>
                {
                    if (args.Source is Window window)
                        UiLocalizer.Apply(window);
                }));
            CaptureExclusion.SyncOpenWindows(_settingsService.CurrentSettings.HideSharpShotWindowsDuringCapture);
            
            // Ensure default save directory exists
            EnsureDefaultSaveDirectoryExists();

            // Clean up any backup exe left behind by a previous update swap.
            CleanupUpdateBackups();
            
            // Debug: Verify settings are loaded
            System.Diagnostics.Debug.WriteLine($"App startup - Settings loaded - IconColor: {_settingsService.CurrentSettings.IconColor}, SavePath: {_settingsService.CurrentSettings.SavePath}");
            
            // Initialize hotkeys
            _hotkeyManager.Initialize();

            // Track last clicked/activated non-SharpShot window for region select targeting
            LastExternalWindowTracker.Start();

            // Prefetch DXGI sessions when GPU capture is already enabled so the first
            // region hotkey is not a cold DuplicateOutput hit.
            if (_settingsService.CurrentSettings.UseDxgiCapture)
            {
                _ = Task.Run(() => DxgiDesktopCapture.Warmup());
            }

            // Check for updates in background (GitHub builds only).
            if (!BuildInfo.DisableInAppUpdates && _settingsService.CurrentSettings.EnableAutoUpdateCheck)
            {
                Task.Run(async () => await CheckForUpdatesAsync());
            }

            // Write themed .ico + refresh Start Menu / pinned taskbar shortcuts (off UI).
            var iconColor = _settingsService.CurrentSettings.IconColor;
            _ = Task.Run(() => PinnedTaskbarIconService.SyncThemedPinnedIcon(iconColor));

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        /// <summary>
        /// A second launch (Run SharpShot, a leftover watch process, or a double Start Menu click)
        /// used to open another copy. Keep the one already on screen and bring it forward.
        /// </summary>
        private static bool TryBecomeOnlyInstance()
        {
            try
            {
                _instanceMutex = new Mutex(true, @"Local\BMO.SharpShot.SingleInstance", out var createdNew);
                return createdNew;
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
            catch
            {
                return true;
            }
        }

        private static void ActivateExistingInstance()
        {
            try
            {
                var currentId = Environment.ProcessId;
                foreach (var process in Process.GetProcessesByName("SharpShot"))
                {
                    if (process.Id == currentId)
                        continue;
                    var handle = process.MainWindowHandle;
                    if (handle == IntPtr.Zero)
                        continue;
                    ShowWindow(handle, 9);
                    SetForegroundWindow(handle);
                    return;
                }
            }
            catch
            {
                // The extra process is exiting either way.
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private void OnAnyWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (e.Source is not Window window || _settingsService?.CurrentSettings == null)
                return;
            if (!CaptureExclusion.IsSharpShotWindow(window))
                return;
            if (_settingsService.CurrentSettings.HideSharpShotWindowsDuringCapture)
                CaptureExclusion.TrySet(window, true);
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

        private void CleanupUpdateBackups()
        {
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                if (string.IsNullOrEmpty(appDir) || !Directory.Exists(appDir))
                {
                    return;
                }

                foreach (var backup in Directory.EnumerateFiles(appDir, "SharpShot.exe.*.old"))
                {
                    try
                    {
                        File.Delete(backup);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not delete update backup '{backup}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CleanupUpdateBackups failed: {ex.Message}");
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
            LastExternalWindowTracker.Stop();
            
            base.OnExit(e);
        }
    }
} 