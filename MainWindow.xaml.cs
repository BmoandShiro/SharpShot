using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SharpShot.Services;
using SharpShot.Utils;
using System.Threading.Tasks;
using Point = System.Windows.Point;

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
        private bool _isInCaptureMode = false;

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

        private async void RecordingButton_Click(object sender, RoutedEventArgs e)
        {
            await ToggleRecording();
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
                    ShowCaptureOptions();
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
                _isInCaptureMode = true;
                
                // Hide normal buttons
                RegionButton.Visibility = Visibility.Collapsed;
                ScreenshotButton.Visibility = Visibility.Collapsed;
                RecordingButton.Visibility = Visibility.Collapsed;
                SettingsButton.Visibility = Visibility.Collapsed;
                CloseButton.Visibility = Visibility.Collapsed;
                
                // Show capture option buttons
                CancelButton.Visibility = Visibility.Visible;
                CopyButton.Visibility = Visibility.Visible;
                SaveButton.Visibility = Visibility.Visible;
            });
        }

        private void ShowNormalButtons()
        {
            Dispatcher.Invoke(() =>
            {
                _isInCaptureMode = false;
                
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
                    _screenshotService.CopyToClipboard(_lastCapturedBitmap);
                }
                else if (!string.IsNullOrEmpty(_lastCapturedFilePath))
                {
                    _screenshotService.CopyToClipboard(_lastCapturedFilePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Dispose of the bitmap and show normal buttons
                _lastCapturedBitmap?.Dispose();
                _lastCapturedBitmap = null;
                _lastCapturedFilePath = string.Empty;
                ShowNormalButtons();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? filePath = null;
                
                if (_lastCapturedBitmap != null)
                {
                    filePath = _screenshotService.SaveScreenshot(_lastCapturedBitmap);
                }
                else if (!string.IsNullOrEmpty(_lastCapturedFilePath))
                {
                    // File is already saved, just show the path
                    filePath = _lastCapturedFilePath;
                }
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    var result = MessageBox.Show(
                        $"Screenshot saved to:\n{filePath}\n\nWould you like to open the folder?",
                        "SharpShot",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        var folderPath = System.IO.Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(folderPath))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", folderPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save screenshot: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Dispose of the bitmap and show normal buttons
                _lastCapturedBitmap?.Dispose();
                _lastCapturedBitmap = null;
                _lastCapturedFilePath = string.Empty;
                ShowNormalButtons();
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
                    ShowNotification("Recording stopped!");
                }
                else
                {
                    await _recordingService.StartRecording();
                    ShowNotification("Recording started!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Recording toggle failed: {ex.Message}");
                ShowNotification("Recording failed!", isError: true);
            }
        }

        private void OnRecordingStateChanged(bool isRecording)
        {
            Dispatcher.Invoke(() =>
            {
                if (isRecording)
                {
                    RecordingTimer.Visibility = Visibility.Visible;
                    RecordingButton.Content = "⏹️";
                }
                else
                {
                    RecordingTimer.Visibility = Visibility.Collapsed;
                    RecordingButton.Content = "🎥";
                }
            });
        }

        private void OnRecordingTimeUpdated(TimeSpan duration)
        {
            Dispatcher.Invoke(() =>
            {
                RecordingTimer.Text = $"{duration:mm\\:ss}";
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
        #endregion
    }
} 