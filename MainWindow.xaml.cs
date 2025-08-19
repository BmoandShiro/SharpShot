using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using SharpShot.Services;
using SharpShot.Utils;
using System.Threading.Tasks;
using Point = System.Windows.Point;
using System.IO;
using System.Runtime.InteropServices;

namespace SharpShot
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly ScreenshotService _screenshotService;
        private readonly RecordingService _recordingService;
        private readonly HotkeyManager _hotkeyManager;
        private bool _isDragging;
        private Point _dragStart;
        private string _lastCapturedFilePath = string.Empty;
        private Bitmap? _lastCapturedBitmap = null;

        // Windows message constants
        private const int WM_HOTKEY = 0x0312;

        public MainWindow()
        {
            InitializeComponent();
            
            // Use the shared SettingsService from App.xaml.cs (which has loaded settings)
            _settingsService = App.SettingsService;
            _screenshotService = new ScreenshotService(_settingsService);
            _recordingService = new RecordingService(_settingsService);
            _hotkeyManager = new HotkeyManager(_settingsService);
            
            // Setup event handlers
            SetupEventHandlers();
            
            // Position window
            PositionWindow();
            
            // Force window to be visible and not minimized
            WindowState = WindowState.Normal;
            Visibility = Visibility.Visible;
            
            // Ensure window is on top and visible
            Activate();
            Focus();
            
            // Debug output
            System.Diagnostics.Debug.WriteLine($"SharpShot window created. Position: ({Left}, {Top}), Size: ({Width}, {Height}), State: {WindowState}, Visibility: {Visibility}");
            
            // Apply theme settings
            ApplyThemeSettings();
            
            // Debug: Verify settings are loaded
            System.Diagnostics.Debug.WriteLine($"Settings loaded - IconColor: {_settingsService.CurrentSettings.IconColor}, SavePath: {_settingsService.CurrentSettings.SavePath}");
            
            // Ensure window is visible after a short delay
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (Visibility != Visibility.Visible)
                {
                    Visibility = Visibility.Visible;
                    Activate();
                    Focus();
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            
            // Debug output
            System.Diagnostics.Debug.WriteLine($"SharpShot window created. Position: ({Left}, {Top}), Size: ({Width}, {Height}), State: {WindowState}");
        }

        private void ApplySavedHotkeys()
        {
            try
            {
                // This method does exactly what the settings window does when it opens
                // It copies the saved hotkey settings to the current settings and updates the hotkey manager
                
                System.Diagnostics.Debug.WriteLine("=== APPLYING SAVED HOTKEYS ON STARTUP ===");
                
                // Get the current settings from the service
                var currentSettings = _settingsService.CurrentSettings;
                
                // Debug: Show what hotkeys are currently loaded
                System.Diagnostics.Debug.WriteLine($"Current EnableGlobalHotkeys: {currentSettings.EnableGlobalHotkeys}");
                foreach (var hotkey in currentSettings.Hotkeys)
                {
                    System.Diagnostics.Debug.WriteLine($"Current: {hotkey.Key} = '{hotkey.Value}'");
                }
                
                // The settings are already loaded from the config file by the SettingsService
                // We just need to tell the hotkey manager to use them
                if (_hotkeyManager != null)
                {
                    System.Diagnostics.Debug.WriteLine("Calling _hotkeyManager.UpdateHotkeys() to register hotkeys with Windows");
                    _hotkeyManager.UpdateHotkeys();
                    
                    // Debug the hotkey manager status after update
                    _hotkeyManager.DebugHotkeyStatus();
                }
                
                System.Diagnostics.Debug.WriteLine("=== SAVED HOTKEYS APPLIED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying saved hotkeys: {ex.Message}");
            }
        }



        private void SetupEventHandlers()
        {
            // Window dragging
            MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
            MouseLeftButtonUp += MainWindow_MouseLeftButtonUp;
            MouseMove += MainWindow_MouseMove;
            
            // Recording events
            _recordingService.RecordingStateChanged += OnRecordingStateChanged;
            _recordingService.RecordingTimeUpdated += OnRecordingTimeUpdated;
            
            // Hotkey events
            _hotkeyManager.OnRegionCaptureRequested += OnRegionCaptureRequested;
            _hotkeyManager.OnRegionCaptureCanceled += OnRegionCaptureCanceled;
            _hotkeyManager.OnFullScreenCaptureRequested += OnFullScreenCaptureRequested;
            _hotkeyManager.OnToggleRecordingRequested += OnToggleRecordingRequested;
            _hotkeyManager.OnSaveRequested += OnSaveRequested;
            _hotkeyManager.OnCopyRequested += OnCopyRequested;
            
            // Initialize hotkeys first (this sets _isInitialized = true)
            _hotkeyManager.Initialize();
            
            // NOTE: Hotkeys will be applied AFTER the window handle is set in OnSourceInitialized
        }

        private void PositionWindow()
        {
            try
            {
                // Use the primary screen for reliable positioning
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    // Fallback to first available screen
                    var allScreens = System.Windows.Forms.Screen.AllScreens;
                    if (allScreens.Length > 0)
                    {
                        primaryScreen = allScreens[0];
                    }
                    else
                    {
                        // Last resort - use default positioning
                        WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        return;
                    }
                }
                
                // Position on primary screen, above taskbar
                var screenBounds = primaryScreen.Bounds;
                
                // Center horizontally on the primary screen
                Left = screenBounds.X + (screenBounds.Width - Width) / 2;
                
                // Position above taskbar (typically 40-50 pixels from bottom)
                Top = screenBounds.Y + screenBounds.Height - Height - 50;
                
                // Ensure window is within the primary screen bounds
                if (Left < screenBounds.X) Left = screenBounds.X;
                if (Top < screenBounds.Y) Top = screenBounds.Y;
                if (Left + Width > screenBounds.X + screenBounds.Width) Left = screenBounds.X + screenBounds.Width - Width;
                if (Top + Height > screenBounds.Y + screenBounds.Height) Top = screenBounds.Y + screenBounds.Height - Height;
                
                // Debug output for positioning
                System.Diagnostics.Debug.WriteLine($"Positioning window: Primary screen bounds: {screenBounds}, Window size: {Width}x{Height}, Position: ({Left}, {Top})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Positioning failed: {ex.Message}");
                // Fallback to center screen if positioning fails
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        #region Window Dragging
        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStart = e.GetPosition(this);
            CaptureMouse();
        }

        private void MainWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            ReleaseMouseCapture();
        }

        private void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPosition = e.GetPosition(this);
                var offset = currentPosition - _dragStart;
                
                Left += offset.X;
                Top += offset.Y;
            }
        }
        #endregion

        #region Button Event Handlers
        private async void RegionButton_Click(object sender, RoutedEventArgs e)
        {
            await CaptureRegion();
        }

        private async void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            await CaptureFullScreen();
        }

        private void RecordingButton_Click(object sender, RoutedEventArgs e)
        {
            ShowRecordingOptions();
        }

        private async void RegionRecordButton_Click(object sender, RoutedEventArgs e)
        {
            await StartRegionRecording();
        }

        private async void FullScreenRecordButton_Click(object sender, RoutedEventArgs e)
        {
            await StartFullScreenRecording();
        }

        private async void OBSRecordButton_Click(object sender, RoutedEventArgs e)
        {
            await OpenOBSStudio();
        }

        private void CancelRecordButton_Click(object sender, RoutedEventArgs e)
        {
            ShowNormalButtons();
        }

        private async void StopRecordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                                        // Ensure FFmpeg is selected for stop recording since OBS is handled in its GUI
                        _settingsService.CurrentSettings.RecordingEngine = "FFmpeg";
                        _settingsService.SaveSettings();
                
                await _recordingService.StopRecording();
                
                // Return to main home page after stopping
                ShowNormalButtons();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop recording failed: {ex.Message}");
                ShowNotification("Stop recording failed!", isError: true);
                ShowNormalButtons();
            }
        }

        private async void PauseRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_recordingService != null && _recordingService.IsRecording)
            {
                // Stop the recording (don't treat exceptions as errors when canceling)
                try
                {
                    await _recordingService.StopRecording();
                }
                catch (Exception ex)
                {
                    // Log the exception but don't show error to user when canceling
                    System.Diagnostics.Debug.WriteLine($"Stop recording during cancel: {ex.Message}");
                }
                
                // Delete the recorded video file
                var recordingPath = _recordingService.GetCurrentRecordingPath();
                if (!string.IsNullOrEmpty(recordingPath) && File.Exists(recordingPath))
                {
                    try
                    {
                        File.Delete(recordingPath);
                        System.Diagnostics.Debug.WriteLine($"Deleted recording file: {recordingPath}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete recording file: {ex.Message}");
                    }
                }
                
                // Return to recording selection menu
                ShowRecordingSelectionButtons();
            }
        }

        private void ShowRecordingOptions()
        {
            try
            {
                // Show recording options (region or full screen) without hiding the window
                ShowRecordingSelectionButtons();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Recording options failed: {ex.Message}");
                ShowNotification("Recording options failed!", isError: true);
            }
        }

        private void ShowRecordingSelectionButtons()
        {
            Dispatcher.Invoke(() =>
            {
                // Hide normal buttons
                RegionButton.Visibility = Visibility.Collapsed;
                ScreenshotButton.Visibility = Visibility.Collapsed;
                RecordingButton.Visibility = Visibility.Collapsed;
                SettingsButton.Visibility = Visibility.Collapsed;
                CloseButton.Visibility = Visibility.Collapsed;

                // Hide main toolbar separators
                MainToolbarSeparator1.Visibility = Visibility.Collapsed;
                MainToolbarSeparator2.Visibility = Visibility.Collapsed;

                // Show recording selection buttons
                RegionRecordButton.Visibility = Visibility.Visible;
                FullScreenRecordButton.Visibility = Visibility.Visible;
                OBSRecordButton.Visibility = Visibility.Visible;
                
                // Show separators between recording selection buttons for proper spacing
                RecordingSelectionSeparator2.Visibility = Visibility.Visible;
                RecordingSelectionSeparator3.Visibility = Visibility.Visible;
                
                // Show cancel button on the far right
                CancelRecordButton.Visibility = Visibility.Visible;
            });
        }

        private void ShowRecordingControls()
        {
            Dispatcher.Invoke(() =>
            {
                // Hide all other buttons first
                RegionButton.Visibility = Visibility.Collapsed;
                ScreenshotButton.Visibility = Visibility.Collapsed;
                RecordingButton.Visibility = Visibility.Collapsed;
                SettingsButton.Visibility = Visibility.Collapsed;
                CloseButton.Visibility = Visibility.Collapsed;
                
                // Hide main toolbar separators
                MainToolbarSeparator1.Visibility = Visibility.Collapsed;
                MainToolbarSeparator2.Visibility = Visibility.Collapsed;
                
                // Hide recording selection buttons
                RegionRecordButton.Visibility = Visibility.Collapsed;
                FullScreenRecordButton.Visibility = Visibility.Collapsed;
                OBSRecordButton.Visibility = Visibility.Collapsed;
                CancelRecordButton.Visibility = Visibility.Collapsed;
                
                // Hide recording selection separators
                RecordingSelectionSeparator2.Visibility = Visibility.Collapsed;
                RecordingSelectionSeparator3.Visibility = Visibility.Collapsed;
                
                // Hide capture option buttons
                CancelButton.Visibility = Visibility.Collapsed;
                CopyButton.Visibility = Visibility.Collapsed;
                SaveButton.Visibility = Visibility.Collapsed;
                CaptureCompletionSeparator1.Visibility = Visibility.Collapsed;
                
                // Show recording control buttons
                StopRecordButton.Visibility = Visibility.Visible;
                PauseRecordButton.Visibility = Visibility.Visible; // This is now the cancel button
                
                // Show recording timer
                RecordingTimer.Visibility = Visibility.Visible;

                // Apply theme-aware styling to recording control buttons
                UpdatePostCaptureButtonStyles();
            });
        }

        private void ShowRecordingCompletionOptions()
        {
            Dispatcher.Invoke(() =>
            {
                // Hide recording selection buttons
                RegionRecordButton.Visibility = Visibility.Collapsed;
                FullScreenRecordButton.Visibility = Visibility.Collapsed;
                CancelRecordButton.Visibility = Visibility.Collapsed;

                // Hide separators
                RecordingSelectionSeparator2.Visibility = Visibility.Collapsed;
                RecordingSelectionSeparator3.Visibility = Visibility.Collapsed;

                // Hide normal buttons
                RegionButton.Visibility = Visibility.Collapsed;
                ScreenshotButton.Visibility = Visibility.Collapsed;
                RecordingButton.Visibility = Visibility.Collapsed;
                SettingsButton.Visibility = Visibility.Collapsed;
                CloseButton.Visibility = Visibility.Collapsed;

                // Show completion options for video
                CancelButton.Visibility = Visibility.Visible;
                CopyButton.Visibility = Visibility.Visible;
                SaveButton.Visibility = Visibility.Visible;

                // Apply theme-aware styling to the buttons
                UpdatePostCaptureButtonStyles();

                // Set video-specific tooltips
                CopyButton.ToolTip = "Copy Video (Not supported)";
                SaveButton.ToolTip = "Save Video";
            });
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new UI.SettingsWindow(_settingsService, _hotkeyManager);
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        #endregion

        #region Screenshot Methods
        private async void OnRegionCaptureRequested()
        {
            await CaptureRegion();
        }

        private void OnRegionCaptureCanceled()
        {
            // Cancel any ongoing region selection
            // This will be called when F18 is pressed twice
            System.Diagnostics.Debug.WriteLine("Region capture canceled by double-press of F18");
            
            // Cancel the active region selection window if it exists
            UI.RegionSelectionWindow.CancelActiveInstance();
        }

        private async void OnFullScreenCaptureRequested()
        {
            await CaptureFullScreen();
        }

        private async Task CaptureFullScreen()
        {
            try
            {
                // Hide window temporarily
                Visibility = Visibility.Hidden;
                await Task.Delay(100); // Brief delay to ensure window is hidden
                
                // Use the screenshot service to capture based on selected screen
                var filePath = _screenshotService.CaptureFullScreen();
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    // Load the captured bitmap for copying
                    using var bitmap = new Bitmap(filePath);
                    _lastCapturedBitmap = new Bitmap(bitmap);
                    _lastCapturedFilePath = filePath;
                    ShowCaptureOptions();
                }
                
                Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Screenshot failed: {ex.Message}");
                ShowNotification("Screenshot failed!", isError: true);
                Visibility = Visibility.Visible;
            }
        }

        private Rectangle GetVirtualDesktopBounds(System.Windows.Forms.Screen[] screens)
        {
            if (screens.Length == 0)
            {
                // Fallback to primary screen if no screens detected
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    // Ultimate fallback - return a default rectangle
                    return new Rectangle(0, 0, 1920, 1080);
                }
                return primaryScreen.Bounds;
            }

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var screen in screens)
            {
                minX = Math.Min(minX, screen.Bounds.X);
                minY = Math.Min(minY, screen.Bounds.Y);
                maxX = Math.Max(maxX, screen.Bounds.X + screen.Bounds.Width);
                maxY = Math.Max(maxY, screen.Bounds.Y + screen.Bounds.Height);
            }

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        private async Task CaptureRegion()
        {
            try
            {
                // Hide main window
                Visibility = Visibility.Hidden;
                await Task.Delay(100);
                
                // Show region selection window
                var regionWindow = new UI.RegionSelectionWindow(_screenshotService, _settingsService);
                
                // Set the hotkey toggle state to indicate region selection is active
                _hotkeyManager.SetRegionSelectionActive();
                
                // Subscribe to the canceled event to reset the hotkey toggle state
                regionWindow.OnRegionSelectionCanceled += () =>
                {
                    System.Diagnostics.Debug.WriteLine("RegionSelectionCanceled event received in MainWindow");
                    _hotkeyManager.ResetRegionSelectionToggle();
                };
                
                regionWindow.ShowDialog();
                
                // Check if a region was captured
                if (regionWindow.CapturedBitmap != null)
                {
                    _lastCapturedBitmap = regionWindow.CapturedBitmap;
                    System.Diagnostics.Debug.WriteLine($"Region captured successfully: {_lastCapturedBitmap.Width}x{_lastCapturedBitmap.Height}");
                    
                    // Check if user completed an action in the editor
                    if (IsEditorActionCompleted(regionWindow))
                    {
                        // User saved or copied in editor - handle accordingly
                        if (regionWindow.CapturedBitmap != null)
                        {
                            // Check what action was completed
                            if (regionWindow.EditorCopyRequested)
                            {
                                // User clicked copy in editor - automatically trigger copy using our working method
                                System.Diagnostics.Debug.WriteLine("Editor copy requested - automatically triggering copy operation");
                                
                                // Use the working MSIX-compatible copy method
                                _screenshotService.CopyToClipboard(_lastCapturedBitmap);
                                
                                // Show success notification
                                ShowNotification("Screenshot copied to clipboard!", isError: false);
                                
                                // Don't show capture options since copy is already done
                                return;
                            }
                            else if (regionWindow.EditorSaveRequested)
                            {
                                // User saved in editor - show success notification
                                System.Diagnostics.Debug.WriteLine("Editor save completed");
                                ShowNotification("Screenshot saved!", isError: false);
                                
                                // Don't show capture options since save is already done
                                return;
                            }
                        }
                    }
                    
                    // Show capture options for normal cases
                    ShowCaptureOptions();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No bitmap captured from region selection");
                }
                
                // Reset the hotkey toggle state since region selection is complete
                _hotkeyManager.ResetRegionSelectionToggle();
                
                // Show main window again
                Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Region capture failed: {ex.Message}");
                // Commented out false alarm - this can trigger when editor copy/save is successful
                // ShowNotification("Region capture failed!", isError: true);
                
                // Reset the hotkey toggle state since region selection failed
                _hotkeyManager.ResetRegionSelectionToggle();
                
                Visibility = Visibility.Visible;
            }
        }

        private bool IsEditorActionCompleted(UI.RegionSelectionWindow regionWindow)
        {
            return regionWindow.EditorActionCompleted;
        }

        private void ShowCaptureOptions()
        {
            Dispatcher.Invoke(() =>
            {
                // Auto-copy if enabled
                if (_settingsService.CurrentSettings.AutoCopyScreenshots && _lastCapturedBitmap != null)
                {
                    AutoCopyScreenshot();
                }
                
                // Hide normal buttons
                RegionButton.Visibility = Visibility.Collapsed;
                ScreenshotButton.Visibility = Visibility.Collapsed;
                RecordingButton.Visibility = Visibility.Collapsed;
                SettingsButton.Visibility = Visibility.Collapsed;
                CloseButton.Visibility = Visibility.Collapsed;

                // Hide main toolbar separators
                MainToolbarSeparator1.Visibility = Visibility.Collapsed;
                MainToolbarSeparator2.Visibility = Visibility.Collapsed;

                // Show completion options in correct order: Copy, Save, Separator, Cancel (X on far right)
                CopyButton.Visibility = Visibility.Visible;
                SaveButton.Visibility = Visibility.Visible;
                CaptureCompletionSeparator1.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;

                // Apply theme-aware styling to the buttons
                UpdatePostCaptureButtonStyles();

                // Set appropriate tooltips based on file type
                bool isVideo = !string.IsNullOrEmpty(_lastCapturedFilePath) && 
                              _lastCapturedFilePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
                
                if (isVideo)
                {
                    CopyButton.ToolTip = "Copy Video (Not supported)";
                    SaveButton.ToolTip = "Save Video";
                }
                else
                {
                    CopyButton.ToolTip = "Copy Screenshot to Clipboard";
                    SaveButton.ToolTip = "Save Screenshot";
                }
            });
        }

        private void ShowNormalButtons()
        {
            Dispatcher.Invoke(() =>
            {
                // Hide recording control buttons first
                StopRecordButton.Visibility = Visibility.Collapsed;
                PauseRecordButton.Visibility = Visibility.Collapsed; // This is now the cancel button
                RecordingTimer.Visibility = Visibility.Collapsed;
                
                // Show normal buttons
                RegionButton.Visibility = Visibility.Visible;
                ScreenshotButton.Visibility = Visibility.Visible;
                RecordingButton.Visibility = Visibility.Visible;
                SettingsButton.Visibility = Visibility.Visible;
                CloseButton.Visibility = Visibility.Visible;
                
                // Reset recording button content to the original Path (video camera icon)
                // The content is already set correctly in XAML, so we don't need to change it
                
                // Show main toolbar separators
                MainToolbarSeparator1.Visibility = Visibility.Visible;
                MainToolbarSeparator2.Visibility = Visibility.Visible;
                
                // Hide capture option buttons
                CancelButton.Visibility = Visibility.Collapsed;
                CopyButton.Visibility = Visibility.Collapsed;
                SaveButton.Visibility = Visibility.Collapsed;
                CaptureCompletionSeparator1.Visibility = Visibility.Collapsed;
                
                // Hide recording selection buttons
                RegionRecordButton.Visibility = Visibility.Collapsed;
                FullScreenRecordButton.Visibility = Visibility.Collapsed;
                OBSRecordButton.Visibility = Visibility.Collapsed;
                CancelRecordButton.Visibility = Visibility.Collapsed;

                // Hide recording selection separators
                RecordingSelectionSeparator2.Visibility = Visibility.Collapsed;
                RecordingSelectionSeparator3.Visibility = Visibility.Collapsed;
            });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Dispose of the bitmap and show normal buttons
            _lastCapturedBitmap?.Dispose();
            _lastCapturedBitmap = null;
            _lastCapturedFilePath = string.Empty;
            ShowNormalButtons();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastCapturedBitmap != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Copying bitmap: {_lastCapturedBitmap.Width}x{_lastCapturedBitmap.Height}");
                    LogToFile($"Copying bitmap: {_lastCapturedBitmap.Width}x{_lastCapturedBitmap.Height}");
                    
                    // Run the copy operation on the UI thread since clipboard requires STA mode
                    _screenshotService.CopyToClipboard(_lastCapturedBitmap);
                    
                    System.Diagnostics.Debug.WriteLine("Copy operation completed successfully");
                    LogToFile("Copy operation completed successfully");
                    
                    // Verify clipboard has data
                    if (System.Windows.Clipboard.ContainsImage())
                    {
                        System.Diagnostics.Debug.WriteLine("Clipboard verification successful - image data is present");
                        LogToFile("Clipboard verification successful - image data is present");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Warning: Clipboard verification failed - no image data found");
                        LogToFile("Warning: Clipboard verification failed - no image data found");
                        ShowNotification("Copy completed but verification failed", isError: true);
                    }
                }
                else if (!string.IsNullOrEmpty(_lastCapturedFilePath))
                {
                    // Check if it's a video file (recording)
                    if (_lastCapturedFilePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                    {
                        // For videos, we can't copy to clipboard, so just show a message
                        ShowNotification("Videos cannot be copied to clipboard. Use the Save button to save the video file.", isError: false);
                    }
                    else
                    {
                        // For screenshots, copy the file to clipboard using the service method
                        _screenshotService.CopyToClipboard(_lastCapturedFilePath);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No bitmap or file path available for copying");
                    ShowNotification("No image available to copy!", isError: true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Copy failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                LogToFile($"Copy failed: {ex.Message}");
                LogToFile($"Stack trace: {ex.StackTrace}");
                
                // Check if clipboard actually has the image despite the exception
                if (System.Windows.Clipboard.ContainsImage())
                {
                    System.Diagnostics.Debug.WriteLine("Clipboard verification successful despite exception");
                    LogToFile("Clipboard verification successful despite exception");
                }
                else
                {
                    ShowNotification("Copy failed!", isError: true);
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? filePath = null;

                if (_lastCapturedBitmap != null)
                {
                    // Save screenshot from bitmap
                    filePath = _screenshotService.SaveScreenshot(_lastCapturedBitmap);
                }
                else if (!string.IsNullOrEmpty(_lastCapturedFilePath))
                {
                    // File is already saved, just use the existing path
                    filePath = _lastCapturedFilePath;
                }

                if (!string.IsNullOrEmpty(filePath))
                {
                    // Show success popup if popups are enabled
                    if (!_settingsService.CurrentSettings.DisableAllPopups)
                    {
                        var fileType = filePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ? "Recording" : "Screenshot";
                        var result = MessageBox.Show(
                            $"{fileType} saved to:\n{filePath}\n\nWould you like to open the folder?",
                            "File Saved",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            var folderPath = Path.GetDirectoryName(filePath);
                            if (!string.IsNullOrEmpty(folderPath))
                            {
                                System.Diagnostics.Process.Start("explorer.exe", folderPath);
                            }
                        }
                    }
                    else
                    {
                        // Log the save operation instead of showing popup
                        System.Diagnostics.Debug.WriteLine($"File saved (popup disabled): {filePath}");
                        LogToFile($"File saved (popup disabled): {filePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save failed: {ex.Message}");
                ShowNotification("Save failed!", isError: true);
            }
        }
        #endregion

        #region Recording Methods
        private async void OnToggleRecordingRequested()
        {
            await ToggleRecording();
        }

        private async Task ToggleRecording()
        {
            try
            {
                if (_recordingService.IsRecording)
                {
                    await _recordingService.StopRecording();
                    // Return to main home page
                    ShowNormalButtons();
                }
                else
                {
                    // If not recording, show recording options instead of starting directly
                    ShowRecordingOptions();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Recording toggle failed: {ex.Message}");
                ShowNotification("Recording failed!", isError: true);
            }
        }

                private Task StartRegionRecording()
        {
            // Show region selection window for recording (without hiding main window)
            var regionWindow = new UI.RegionSelectionWindow(_screenshotService, _settingsService, isRecordingMode: true);
            regionWindow.ShowDialog();
            
            // Check if a region was selected
            if (regionWindow.SelectedRegion.HasValue)
            {
                // Update UI immediately to show recording controls
                Dispatcher.Invoke(() =>
                {
                    // Use the proper recording controls method
                    ShowRecordingControls();
                }, System.Windows.Threading.DispatcherPriority.Render);
                
                // Start recording in the background (don't await it)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Ensure FFmpeg is selected for region recording
                        _settingsService.CurrentSettings.RecordingEngine = "FFmpeg";
                        _settingsService.SaveSettings();
                        
                        await _recordingService.StartRecording(regionWindow.SelectedRegion.Value);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Background recording start failed: {ex.Message}");
                        // Don't show error to user since recording might still work
                        // The OnRecordingStateChanged event will handle UI updates
                    }
                });
            }
            else
            {
                // If no region selected, go back to normal buttons
                ShowNormalButtons();
            }
            
            return Task.CompletedTask;
        }

        private Task StartFullScreenRecording()
        {
            // Update UI immediately to show recording controls
            Dispatcher.Invoke(() =>
            {
                // Use the proper recording controls method
                ShowRecordingControls();
            }, System.Windows.Threading.DispatcherPriority.Render);
            
            // Start recording in the background (don't await it)
            _ = Task.Run(async () =>
            {
                try
                {
                    // Ensure FFmpeg is selected for full screen recording
                    _settingsService.CurrentSettings.RecordingEngine = "FFmpeg";
                    _settingsService.SaveSettings();
                    
                    await _recordingService.StartRecording(); // Use null to let the service determine bounds based on selected screen
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Background recording start failed: {ex.Message}");
                    // Don't show error to user since recording might still work
                    // The OnRecordingStateChanged event will handle UI updates
                }
            });
            
            return Task.CompletedTask;
        }

        private string? _originalRecordingEngine = null; // Track original engine
        
        private async Task OpenOBSStudio()
        {
            try
            {
                // Just launch OBS - no recording engine switching or recording needed
                var success = await _recordingService.SetupOBSForRecordingAsync();
                if (success)
                {
                    // OBS launched successfully - user controls everything through OBS GUI
                    ShowNormalButtons();
                }
                else
                {
                    ShowNormalButtons();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OBS launch failed: {ex.Message}");
                ShowNormalButtons();
            }
        }
        
        private void RestoreOriginalRecordingEngine()
        {
            if (_originalRecordingEngine != null)
            {
                _settingsService.CurrentSettings.RecordingEngine = _originalRecordingEngine;
                _originalRecordingEngine = null;
            }
        }

        private void OnRecordingStateChanged(object? sender, bool isRecording)
        {
            Dispatcher.Invoke(() =>
            {
                if (isRecording)
                {
                    // Use the proper recording controls method
                    ShowRecordingControls();
                }
                else
                {
                    // Store the recording file path for save/copy options
                    if (_recordingService != null)
                    {
                        _lastCapturedFilePath = _recordingService.GetCurrentRecordingPath() ?? string.Empty;
                    }
                    
                    // If we were using OBS temporarily, restore original engine
                    // (This handles cases where OBS recording ends from within OBS GUI)
                    RestoreOriginalRecordingEngine();
                    
                    // Return to main home page when recording stops
                    ShowNormalButtons();
                }
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private void OnRecordingTimeUpdated(object? sender, TimeSpan duration)
        {
            // Reduce update frequency to minimize flickering
            Dispatcher.Invoke(() =>
            {
                // Only update if the text actually changed to reduce unnecessary redraws
                var newText = $"{duration:mm\\:ss}";
                if (RecordingTimer.Text != newText)
                {
                    RecordingTimer.Text = newText;
                }
            });
        }
        #endregion

        #region Hotkey Handlers
        private void OnSaveRequested()
        {
            // TODO: Implement save functionality for annotation mode
        }

        private void OnCopyRequested()
        {
            // TODO: Implement copy functionality for annotation mode
        }
        #endregion

        #region Utility Methods
        private void AutoCopyScreenshot()
        {
            try
            {
                if (_lastCapturedBitmap != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-copying bitmap: {_lastCapturedBitmap.Width}x{_lastCapturedBitmap.Height}");
                    LogToFile($"Auto-copying bitmap: {_lastCapturedBitmap.Width}x{_lastCapturedBitmap.Height}");
                    
                    // Run the copy operation on the UI thread since clipboard requires STA mode
                    _screenshotService.CopyToClipboard(_lastCapturedBitmap);
                    
                    System.Diagnostics.Debug.WriteLine("Auto-copy operation completed successfully");
                    LogToFile("Auto-copy operation completed successfully");
                    
                    // Verify clipboard has data
                    if (System.Windows.Clipboard.ContainsImage())
                    {
                        System.Diagnostics.Debug.WriteLine("Auto-copy clipboard verification successful - image data is present");
                        LogToFile("Auto-copy clipboard verification successful - image data is present");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Warning: Auto-copy clipboard verification failed - no image data found");
                        LogToFile("Warning: Auto-copy clipboard verification failed - no image data found");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-copy failed: {ex.Message}");
                LogToFile($"Auto-copy failed: {ex.Message}");
                // Don't show error notification for auto-copy to avoid interrupting user workflow
            }
        }

        private void ShowNotification(string message, bool isError = false)
        {
            // Check if popups are disabled
            if (_settingsService.CurrentSettings.DisableAllPopups)
            {
                // Just log the message instead of showing popup
                System.Diagnostics.Debug.WriteLine($"Notification (popup disabled): {message}");
                LogToFile($"Notification (popup disabled): {message}");
                return;
            }

            // TODO: Implement proper toast notification
            // For now, just show a message box
            var icon = isError ? MessageBoxImage.Error : MessageBoxImage.Information;
            MessageBox.Show(message, "SharpShot", MessageBoxButton.OK, icon);
        }

        private void LogToFile(string message)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sharpshot_debug.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public void ApplyThemeSettings()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Apply icon color
                    var iconColor = _settingsService.CurrentSettings.IconColor;
                    if (!string.IsNullOrEmpty(iconColor))
                    {
                        UpdateIconColors(iconColor);
                    }
                    
                    // Apply hover opacity
                    var hoverOpacity = _settingsService.CurrentSettings.HoverOpacity;
                    UpdateHoverOpacity(hoverOpacity);
                    
                    // Apply drop shadow opacity
                    var dropShadowOpacity = _settingsService.CurrentSettings.DropShadowOpacity;
                    UpdateDropShadowOpacity(dropShadowOpacity);

                    // Update post-capture button styles with new theme color
                    UpdatePostCaptureButtonStyles();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply theme settings: {ex.Message}");
            }
        }
        
        public void UpdateHotkeys()
        {
            try
            {
                _hotkeyManager?.UpdateHotkeys();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update hotkeys: {ex.Message}");
            }
        }

        private void UpdateIconColors(string color)
        {
            try
            {
                // Update the global AccentBrush resource so all XAML elements using {DynamicResource AccentBrush} update automatically
                var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                Application.Current.Resources["AccentBrush"] = brush;
                
                // Update all icon paths to use the new color
                if (RegionButton.Content is System.Windows.Shapes.Path regionPath)
                    regionPath.Stroke = brush;
                
                if (ScreenshotButton.Content is System.Windows.Shapes.Path screenshotPath)
                    screenshotPath.Stroke = brush;
                
                if (RecordingButton.Content is System.Windows.Shapes.Path recordingPath)
                    recordingPath.Stroke = brush;
                
                if (SettingsButton.Content is System.Windows.Shapes.Path settingsPath)
                    settingsPath.Stroke = brush;
                
                if (CloseButton.Content is System.Windows.Shapes.Path closePath)
                    closePath.Stroke = brush;
                
                // Update recording selection icons
                if (RegionRecordButton.Content is System.Windows.Shapes.Path regionRecordPath)
                    regionRecordPath.Stroke = brush;
                
                if (FullScreenRecordButton.Content is System.Windows.Shapes.Path fullScreenRecordPath)
                    fullScreenRecordPath.Stroke = brush;
                
                if (OBSRecordButton.Content is System.Windows.Shapes.Path obsRecordPath)
                    obsRecordPath.Stroke = brush;
                
                if (CancelRecordButton.Content is System.Windows.Shapes.Path cancelRecordPath)
                    cancelRecordPath.Stroke = brush;
                
                // Update recording control icons
                if (StopRecordButton.Content is System.Windows.Shapes.Path stopRecordPath)
                    stopRecordPath.Stroke = brush;
                
                if (PauseRecordButton.Content is System.Windows.Shapes.Path pauseRecordPath)
                    pauseRecordPath.Stroke = brush;
                
                // Update capture option icons
                if (CancelButton.Content is System.Windows.Shapes.Path cancelPath)
                    cancelPath.Stroke = brush;
                
                if (CopyButton.Content is System.Windows.Shapes.Path copyPath)
                    copyPath.Stroke = brush;
                
                if (SaveButton.Content is System.Windows.Shapes.Path savePath)
                    savePath.Stroke = brush;
                
                // Update separator colors
                UpdateSeparatorColors(color);
                
                System.Diagnostics.Debug.WriteLine($"Applied icon color: {color}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update icon colors: {ex.Message}");
            }
        }

        private void UpdateSeparatorColors(string color)
        {
            try
            {
                // Ensure separators always use full opacity by removing any alpha channel
                var fullOpacityColor = color;
                if (color.Length == 9) // Has alpha channel (e.g., #80FF8C00)
                {
                    fullOpacityColor = "#FF" + color.Substring(3); // Force full opacity
                }
                else if (color.Length == 7) // No alpha channel (e.g., #FF8C00)
                {
                    fullOpacityColor = "#FF" + color.Substring(1); // Add full opacity
                }
                
                var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fullOpacityColor));
                
                // Update named separators directly
                if (MainToolbarSeparator1 != null)
                    MainToolbarSeparator1.Fill = brush;
                
                if (MainToolbarSeparator2 != null)
                    MainToolbarSeparator2.Fill = brush;
                
                if (RecordingSelectionSeparator2 != null)
                    RecordingSelectionSeparator2.Fill = brush;
                
                if (RecordingSelectionSeparator3 != null)
                    RecordingSelectionSeparator3.Fill = brush;
                
                if (CaptureCompletionSeparator1 != null)
                    CaptureCompletionSeparator1.Fill = brush;
                
                // Find all Rectangle elements in the main StackPanel that are separators (fallback)
                var mainStackPanel = this.FindName("MainToolbarStackPanel") as StackPanel;
                if (mainStackPanel != null)
                {
                    foreach (var child in mainStackPanel.Children)
                    {
                        if (child is System.Windows.Shapes.Rectangle rectangle && rectangle.Width == 1)
                        {
                            rectangle.Fill = brush;
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Updated separator colors: {color}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update separator colors: {ex.Message}");
            }
        }

        private void UpdateHoverOpacity(double opacity)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Get current icon color
                    var iconColor = _settingsService.CurrentSettings.IconColor;
                    if (string.IsNullOrEmpty(iconColor))
                        iconColor = "#FFFF8C00"; // Default orange
                    
                    // Convert opacity to hex color with alpha channel using the icon color
                    var alpha = (byte)(opacity * 255);
                    // Extract the RGB part of the hex color (remove the # and alpha)
                    var iconColorWithoutAlpha = iconColor.Length == 9 ? iconColor.Substring(3) : iconColor.Substring(1);
                    var hoverColor = $"#{alpha:X2}{iconColorWithoutAlpha}";
                    
                    System.Diagnostics.Debug.WriteLine($"Icon color: {iconColor}, Alpha: {alpha:X2}, Hover color: {hoverColor}");
                    
                    // Create a new style with updated hover opacity
                    var newStyle = CreateUpdatedButtonStyle(hoverColor, _settingsService.CurrentSettings.DropShadowOpacity);
                    
                    // Replace the existing style
                    Application.Current.Resources["ToolbarButtonStyle"] = newStyle;
                    
                    // Force refresh of all buttons
                    RefreshButtonStyles();
                    
                    System.Diagnostics.Debug.WriteLine($"Updated hover opacity: {opacity} -> {hoverColor}");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update hover opacity: {ex.Message}");
            }
        }

        private void UpdateDropShadowOpacity(double opacity)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Get current icon color
                    var iconColor = _settingsService.CurrentSettings.IconColor;
                    if (string.IsNullOrEmpty(iconColor))
                        iconColor = "#FFFF8C00"; // Default orange
                    
                    // Get current hover opacity
                    var hoverOpacity = _settingsService.CurrentSettings.HoverOpacity;
                    var alpha = (byte)(hoverOpacity * 255);
                    // Extract the RGB part of the hex color (remove the # and alpha)
                    var iconColorWithoutAlpha = iconColor.Length == 9 ? iconColor.Substring(3) : iconColor.Substring(1);
                    var hoverColor = $"#{alpha:X2}{iconColorWithoutAlpha}";
                    
                    System.Diagnostics.Debug.WriteLine($"Icon color: {iconColor}, Alpha: {alpha:X2}, Hover color: {hoverColor}");
                    
                    // Create a new style with updated drop shadow opacity
                    var newStyle = CreateUpdatedButtonStyle(hoverColor, opacity);
                    
                    // Replace the existing style
                    Application.Current.Resources["ToolbarButtonStyle"] = newStyle;
                    
                    // Force refresh of all buttons
                    RefreshButtonStyles();
                    
                    System.Diagnostics.Debug.WriteLine($"Updated drop shadow opacity: {opacity}");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update drop shadow opacity: {ex.Message}");
            }
        }

        private Style CreateUpdatedButtonStyle(string hoverColor, double dropShadowOpacity)
        {
            var style = new Style(typeof(Button));
            
            // Base properties - preserve original button styling
            style.Setters.Add(new Setter(Button.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
            style.Setters.Add(new Setter(Button.ForegroundProperty, Application.Current.Resources["TextBrush"]));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(8)));
            style.Setters.Add(new Setter(Button.MarginProperty, new Thickness(4)));
            style.Setters.Add(new Setter(Button.FontSizeProperty, 28.0));
            style.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.Normal));
            style.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
            // Note: RegionButton has Width="70" in XAML, others are 60
            style.Setters.Add(new Setter(Button.HeightProperty, 50.0));
            
            // Template
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            border.AppendChild(contentPresenter);
            template.VisualTree = border;
            
            // Triggers
            var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hoverColor))));
            
            var dropShadow = new DropShadowEffect();
            // Use the same color as the hover background (which is based on icon color)
            var iconColor = _settingsService.CurrentSettings.IconColor;
            if (string.IsNullOrEmpty(iconColor))
                iconColor = "#FFFF8C00"; // Default orange
            dropShadow.Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(iconColor);
            dropShadow.BlurRadius = 10;
            dropShadow.ShadowDepth = 0;
            dropShadow.Opacity = dropShadowOpacity;
            trigger.Setters.Add(new Setter(Button.EffectProperty, dropShadow));
            
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            // Use the same opacity-based approach as the settings buttons
            var pressedColor = $"#30{iconColor.Substring(1)}"; // 30% opacity
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(pressedColor))));
            
            template.Triggers.Add(trigger);
            template.Triggers.Add(pressedTrigger);
            
            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            
            return style;
        }

        private void RefreshButtonStyles()
        {
            // Force all buttons to refresh their styles
            if (RegionButton != null) RegionButton.Style = null;
            if (ScreenshotButton != null) ScreenshotButton.Style = null;
            if (RecordingButton != null) RecordingButton.Style = null;
            if (SettingsButton != null) SettingsButton.Style = null;
            if (CloseButton != null) CloseButton.Style = null;
            
            // Force recording selection buttons to refresh their styles
            if (RegionRecordButton != null) RegionRecordButton.Style = null;
            if (FullScreenRecordButton != null) FullScreenRecordButton.Style = null;
            if (OBSRecordButton != null) OBSRecordButton.Style = null;
            if (CancelRecordButton != null) CancelRecordButton.Style = null;
            
            // Re-apply the style
            var buttonStyle = Application.Current.Resources["ToolbarButtonStyle"] as Style;
            if (buttonStyle != null)
            {
                if (RegionButton != null) 
                {
                    RegionButton.Style = buttonStyle;
                    RegionButton.Width = 70; // Preserve special width for region button
                }
                if (ScreenshotButton != null) 
                {
                    ScreenshotButton.Style = buttonStyle;
                    ScreenshotButton.Width = 60;
                }
                if (RecordingButton != null) 
                {
                    RecordingButton.Style = buttonStyle;
                    RecordingButton.Width = 60;
                }
                if (SettingsButton != null) 
                {
                    SettingsButton.Style = buttonStyle;
                    SettingsButton.Width = 60;
                }
                if (CloseButton != null) 
                {
                    CloseButton.Style = buttonStyle;
                    CloseButton.Width = 60;
                }
                
                // Re-apply style to recording selection buttons
                if (RegionRecordButton != null) 
                {
                    RegionRecordButton.Style = buttonStyle;
                    RegionRecordButton.Width = 60;
                }
                if (FullScreenRecordButton != null) 
                {
                    FullScreenRecordButton.Style = buttonStyle;
                    FullScreenRecordButton.Width = 60;
                }
                if (OBSRecordButton != null) 
                {
                    OBSRecordButton.Style = buttonStyle;
                    OBSRecordButton.Width = 60;
                }
                if (CancelRecordButton != null) 
                {
                    CancelRecordButton.Style = buttonStyle;
                    CancelRecordButton.Width = 60;
                }
            }
        }

        #region Windows Message Handling
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Get the window handle
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var hwnd = helper.Handle;
            
            // Set the window handle in the hotkey manager
            _hotkeyManager.SetWindowHandle(hwnd);
            
            // Add message hook
            System.Windows.Interop.HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            
            // CRITICAL: NOW apply saved hotkeys AFTER the window handle is set
            ApplySavedHotkeys();
            
            // Debug hotkey status after everything is set up
            _hotkeyManager.DebugHotkeyStatus();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                var hotkeyId = wParam.ToInt32();
                _hotkeyManager.HandleHotkeyMessage(hotkeyId);
                handled = true;
                return IntPtr.Zero;
            }
            
            return IntPtr.Zero;
        }
        #endregion
        #endregion

        #region Theme-Aware Button Styling

        private void UpdatePostCaptureButtonStyles()
        {
            if (_settingsService?.CurrentSettings?.IconColor != null)
            {
                var iconColorstr = _settingsService.CurrentSettings.IconColor;
                if (System.Windows.Media.ColorConverter.ConvertFromString(iconColorstr) is System.Windows.Media.Color themeColor)
                {
                    var themeBrush = new System.Windows.Media.SolidColorBrush(themeColor);
                    var hoverBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(32, themeColor.R, themeColor.G, themeColor.B));

                    // Create dynamic styles for post-capture menu buttons
                    CopyButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                    SaveButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                    CancelButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);

                    // Also apply theme-aware styling to recording control buttons
                    StopRecordButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                    PauseRecordButton.Style = CreateThemeAwareButtonStyle(themeColor, hoverBrush);
                }
            }
        }

        private Style CreateThemeAwareButtonStyle(System.Windows.Media.Color themeColor, System.Windows.Media.Brush hoverBrush)
        {
            var iconButtonStyle = this.Resources["IconButtonStyle"] as Style;
            var style = new Style(typeof(Button), iconButtonStyle);

            // Override the hover effect to use theme color with opacity-based approach
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            // Use 15% opacity for hover (same as settings buttons)
            var hoverColor = System.Windows.Media.Color.FromArgb(38, themeColor.R, themeColor.G, themeColor.B); // 15% of 255 ≈ 38
            hoverTrigger.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(hoverColor)));
            hoverTrigger.Setters.Add(new Setter(EffectProperty, new DropShadowEffect
            {
                Color = themeColor,
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.25
            }));

            // Add pressed trigger with 30% opacity (same as settings buttons)
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            var pressedColor = System.Windows.Media.Color.FromArgb(77, themeColor.R, themeColor.G, themeColor.B); // 30% of 255 ≈ 77
            pressedTrigger.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(pressedColor)));

            style.Triggers.Add(hoverTrigger);
            style.Triggers.Add(pressedTrigger);
            return style;
        }

        #endregion
    }
} 