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
            
            // Initialize services
            _settingsService = new SettingsService();
            _screenshotService = new ScreenshotService(_settingsService);
            _recordingService = new RecordingService(_settingsService);
            _hotkeyManager = new HotkeyManager(_settingsService);
            
            // Setup event handlers
            SetupEventHandlers();
            
            // Position window
            PositionWindow();
            
            // Start minimized if setting is enabled
            if (_settingsService.CurrentSettings.StartMinimized)
            {
                WindowState = WindowState.Minimized;
            }
            
            // Apply theme settings
            ApplyThemeSettings();
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
            _hotkeyManager.OnFullScreenCaptureRequested += OnFullScreenCaptureRequested;
            _hotkeyManager.OnToggleRecordingRequested += OnToggleRecordingRequested;
            _hotkeyManager.OnCancelRequested += OnCancelRequested;
            _hotkeyManager.OnSaveRequested += OnSaveRequested;
            _hotkeyManager.OnCopyRequested += OnCopyRequested;
            
            // Initialize hotkeys
            _hotkeyManager.Initialize();
        }

        private void PositionWindow()
        {
            // Position in top-right corner
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            Left = screenWidth - Width - 20;
            Top = 20;
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

        private void CancelRecordButton_Click(object sender, RoutedEventArgs e)
        {
            ShowNormalButtons();
        }

        private async void StopRecordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _recordingService.StopRecording();
                // Show completion options after stopping
                ShowRecordingCompletionOptions();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop recording failed: {ex.Message}");
                ShowNotification("Stop recording failed!", isError: true);
            }
        }

        private void PauseRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_recordingService != null)
            {
                if (_recordingService.IsPaused)
                {
                    _recordingService.ResumeRecording();
                    PauseRecordButton.Content = "⏸";
                    PauseRecordButton.ToolTip = "Pause Recording";
                }
                else
                {
                    _recordingService.PauseRecording();
                    PauseRecordButton.Content = "▶";
                    PauseRecordButton.ToolTip = "Resume Recording";
                }
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

                // Show recording selection buttons
                RegionRecordButton.Visibility = Visibility.Visible;
                FullScreenRecordButton.Visibility = Visibility.Visible;
                CancelRecordButton.Visibility = Visibility.Visible;
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

                // Set video-specific tooltips
                CopyButton.ToolTip = "Copy Video (Not supported)";
                SaveButton.ToolTip = "Save Video";
            });
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new UI.SettingsWindow(_settingsService);
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
                
                // Capture full screen and get both file path and bitmap
                var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                
                // Store the bitmap for copying
                _lastCapturedBitmap = new Bitmap(bitmap);
                
                // Save to file
                var filePath = _screenshotService.SaveScreenshot(bitmap);
                
                if (!string.IsNullOrEmpty(filePath))
                {
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

        private async Task CaptureRegion()
        {
            try
            {
                // Hide main window
                Visibility = Visibility.Hidden;
                await Task.Delay(100);
                
                // Show region selection window
                var regionWindow = new UI.RegionSelectionWindow(_screenshotService);
                regionWindow.ShowDialog();
                
                // Check if a region was captured
                if (regionWindow.CapturedBitmap != null)
                {
                    _lastCapturedBitmap = regionWindow.CapturedBitmap;
                    System.Diagnostics.Debug.WriteLine($"Region captured successfully: {_lastCapturedBitmap.Width}x{_lastCapturedBitmap.Height}");
                    ShowCaptureOptions();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No bitmap captured from region selection");
                }
                
                // Show main window again
                Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Region capture failed: {ex.Message}");
                ShowNotification("Region capture failed!", isError: true);
                Visibility = Visibility.Visible;
            }
        }

        private void ShowCaptureOptions()
        {
            Dispatcher.Invoke(() =>
            {
                // Hide normal buttons
                RegionButton.Visibility = Visibility.Collapsed;
                ScreenshotButton.Visibility = Visibility.Collapsed;
                RecordingButton.Visibility = Visibility.Collapsed;
                SettingsButton.Visibility = Visibility.Collapsed;
                CloseButton.Visibility = Visibility.Collapsed;

                // Show completion options
                CancelButton.Visibility = Visibility.Visible;
                CopyButton.Visibility = Visibility.Visible;
                SaveButton.Visibility = Visibility.Visible;

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
                
                // Show normal buttons
                RegionButton.Visibility = Visibility.Visible;
                ScreenshotButton.Visibility = Visibility.Visible;
                RecordingButton.Visibility = Visibility.Visible;
                SettingsButton.Visibility = Visibility.Visible;
                CloseButton.Visibility = Visibility.Visible;
                
                // Hide capture option buttons
                CancelButton.Visibility = Visibility.Collapsed;
                CopyButton.Visibility = Visibility.Collapsed;
                SaveButton.Visibility = Visibility.Collapsed;
                
                // Hide recording selection buttons
                RegionRecordButton.Visibility = Visibility.Collapsed;
                FullScreenRecordButton.Visibility = Visibility.Collapsed;
                CancelRecordButton.Visibility = Visibility.Collapsed;
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
                    if (System.Windows.Forms.Clipboard.ContainsImage())
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
                if (System.Windows.Forms.Clipboard.ContainsImage())
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
                    // Show recording completion options
                    ShowRecordingCompletionOptions();
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

        private async Task StartRegionRecording()
        {
            try
            {
                // Show region selection window for recording (without hiding main window)
                var regionWindow = new UI.RegionSelectionWindow(_screenshotService);
                regionWindow.ShowDialog();
                
                // Check if a region was selected
                if (regionWindow.SelectedRegion.HasValue)
                {
                    // Update UI immediately to show recording controls
                    Dispatcher.Invoke(() =>
                    {
                        // Show recording control buttons immediately
                        RecordingTimer.Visibility = Visibility.Visible;
                        RecordingButton.Content = "⏹️";
                        StopRecordButton.Visibility = Visibility.Visible;
                        PauseRecordButton.Visibility = Visibility.Visible;
                        
                        // Hide normal buttons immediately
                        RegionButton.Visibility = Visibility.Collapsed;
                        ScreenshotButton.Visibility = Visibility.Collapsed;
                        RecordingButton.Visibility = Visibility.Collapsed;
                        SettingsButton.Visibility = Visibility.Collapsed;
                        CloseButton.Visibility = Visibility.Collapsed;
                    }, System.Windows.Threading.DispatcherPriority.Render);
                    
                    // Start recording in the background (don't await it)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _recordingService.StartRecording(regionWindow.SelectedRegion.Value);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Background recording start failed: {ex.Message}");
                            // If recording fails, revert UI on main thread
                            Dispatcher.Invoke(() =>
                            {
                                ShowNotification("Recording failed to start!", isError: true);
                                ShowNormalButtons();
                            });
                        }
                    });
                }
                else
                {
                    // If no region selected, go back to normal buttons
                    ShowNormalButtons();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Region recording failed: {ex.Message}");
                ShowNotification("Region recording failed!", isError: true);
                ShowNormalButtons();
            }
        }

        private async Task StartFullScreenRecording()
        {
            try
            {
                // Update UI immediately to show recording controls
                Dispatcher.Invoke(() =>
                {
                    // Show recording control buttons immediately
                    RecordingTimer.Visibility = Visibility.Visible;
                    RecordingButton.Content = "⏹️";
                    StopRecordButton.Visibility = Visibility.Visible;
                    PauseRecordButton.Visibility = Visibility.Visible;
                    
                    // Hide normal buttons immediately
                    RegionButton.Visibility = Visibility.Collapsed;
                    ScreenshotButton.Visibility = Visibility.Collapsed;
                    RecordingButton.Visibility = Visibility.Collapsed;
                    SettingsButton.Visibility = Visibility.Collapsed;
                    CloseButton.Visibility = Visibility.Collapsed;
                }, System.Windows.Threading.DispatcherPriority.Render);
                
                // Start recording in the background (don't await it)
                var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _recordingService.StartRecording(new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Background recording start failed: {ex.Message}");
                        // If recording fails, revert UI on main thread
                        Dispatcher.Invoke(() =>
                        {
                            ShowNotification("Recording failed to start!", isError: true);
                            ShowNormalButtons();
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Full screen recording failed: {ex.Message}");
                ShowNotification("Full screen recording failed!", isError: true);
                ShowNormalButtons();
            }
        }

        private void OnRecordingStateChanged(bool isRecording)
        {
            Dispatcher.Invoke(() =>
            {
                if (isRecording)
                {
                    // Batch all UI changes together to reduce flickering
                    RecordingTimer.Visibility = Visibility.Visible;
                    RecordingButton.Content = "⏹️";
                    
                    // Show recording control buttons
                    StopRecordButton.Visibility = Visibility.Visible;
                    PauseRecordButton.Visibility = Visibility.Visible;
                    
                    // Hide normal buttons in one batch
                    RegionButton.Visibility = Visibility.Collapsed;
                    ScreenshotButton.Visibility = Visibility.Collapsed;
                    RecordingButton.Visibility = Visibility.Collapsed;
                    SettingsButton.Visibility = Visibility.Collapsed;
                    CloseButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Batch all UI changes together to reduce flickering
                    RecordingTimer.Visibility = Visibility.Collapsed;
                    RecordingButton.Content = "🎥";
                    
                    // Hide recording control buttons
                    StopRecordButton.Visibility = Visibility.Collapsed;
                    PauseRecordButton.Visibility = Visibility.Collapsed;
                    
                    // Store the recording file path for save/copy options
                    if (_recordingService != null)
                    {
                        _lastCapturedFilePath = _recordingService.GetCurrentRecordingPath();
                    }
                    
                    // Show completion options when recording stops
                    ShowRecordingCompletionOptions();
                }
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private void OnRecordingTimeUpdated(TimeSpan duration)
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
        private void OnCancelRequested()
        {
            // TODO: Implement cancel functionality for annotation mode
        }

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
        private void ShowNotification(string message, bool isError = false)
        {
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
                // Update all icon paths to use the new color
                if (RegionButton.Content is System.Windows.Shapes.Path regionPath)
                    regionPath.Stroke = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                
                if (ScreenshotButton.Content is System.Windows.Shapes.Path screenshotPath)
                    screenshotPath.Stroke = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                
                if (RecordingButton.Content is System.Windows.Shapes.Path recordingPath)
                    recordingPath.Stroke = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                
                if (SettingsButton.Content is System.Windows.Shapes.Path settingsPath)
                    settingsPath.Stroke = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                
                if (CloseButton.Content is System.Windows.Shapes.Path closePath)
                    closePath.Stroke = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                
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
                // Find all Rectangle elements in the main StackPanel that are separators
                var mainStackPanel = this.FindName("MainToolbarStackPanel") as StackPanel;
                if (mainStackPanel != null)
                {
                    foreach (var child in mainStackPanel.Children)
                    {
                        if (child is System.Windows.Shapes.Rectangle rectangle && rectangle.Width == 1)
                        {
                            rectangle.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
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
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, Application.Current.Resources["SecondaryBrush"]));
            
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
    }
} 