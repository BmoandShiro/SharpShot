using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using SharpShot.Services;
using Point = System.Windows.Point;

namespace SharpShot.UI
{
    public partial class RegionSelectionWindow : Window
    {
        // Windows API constants for non-focus-stealing window
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern IntPtr SetCapture(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetCapture();
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        
        private readonly ScreenshotService _screenshotService;
        private readonly SettingsService? _settingsService;
        private static RegionSelectionWindow? _activeInstance;
        
        // Event to notify when region selection is canceled
        public event Action? OnRegionSelectionCanceled;
        
        public static void CancelActiveInstance()
        {
            if (_activeInstance != null)
            {
                // Notify that region selection was canceled
                _activeInstance.OnRegionSelectionCanceled?.Invoke();
                
                // Close the window
                _activeInstance.Close();
                _activeInstance = null;
            }
        }
        
        private Point _startPoint;
        private bool _isSelecting;
        public Rectangle? SelectedRegion { get; private set; }
        public Bitmap? CapturedBitmap { get; private set; }
        public bool EditorActionCompleted { get; private set; } = false;
        public bool EditorCopyRequested { get; private set; } = false;
        public bool EditorSaveRequested { get; private set; } = false;
        private Rectangle _virtualDesktopBounds;
        private MagnifierWindow? _magnifier;
        private System.Windows.Threading.DispatcherTimer? _magnifierTimer;
        private readonly bool _isRecordingMode; // New field to distinguish between screenshot and recording modes

        public RegionSelectionWindow(ScreenshotService screenshotService, SettingsService? settingsService = null, bool isRecordingMode = false)
        {
            InitializeComponent();
            _screenshotService = screenshotService;
            _settingsService = settingsService;
            _isRecordingMode = isRecordingMode; // Store the mode
            
            // Set this as the active instance
            _activeInstance = this;
            
            // Calculate virtual desktop bounds (all monitors combined)
            _virtualDesktopBounds = GetVirtualDesktopBounds();
            
            // Position and size the window to cover all monitors
            PositionWindowForAllMonitors();
            
            // Setup event handlers - use Preview events to capture before browser
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            PreviewMouseMove += OnPreviewMouseMove;
            
            // Also keep the canvas handlers as backup
            SelectionCanvas.MouseLeftButtonDown += OnMouseLeftButtonDown;
            SelectionCanvas.MouseLeftButtonUp += OnMouseLeftButtonUp;
            SelectionCanvas.MouseMove += OnMouseMove;
            
            // Ensure the window can capture keyboard input and connect the KeyDown event
            Focusable = true;
            
            // Set window to capture all keyboard input
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            
            // Connect keyboard events
            KeyDown += OnKeyDown;
            PreviewKeyDown += OnKeyDown;
            
            // Also try to capture keyboard input at the window level
            PreviewKeyUp += (s, e) => { }; // Empty handler to ensure keyboard capture
            
            // Window has ShowActivated="False" in XAML to prevent stealing focus
            // This prevents browser dropdowns from closing when region selection starts
            // We don't force focus here to maintain browser focus
            
            // Test keyboard input by showing a message
            System.Diagnostics.Debug.WriteLine("RegionSelectionWindow constructor completed - testing keyboard input");
            
            // Hide cursor instructions after a moment
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (sender, e) =>
            {
                InstructionsText.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
            
            // Initialize magnifier
            InitializeMagnifier();
            
            // Capture mouse input immediately when window is loaded
            // This ensures mouse clicks go to our overlay, not the browser
            Loaded += (sender, e) =>
            {
                CaptureMouseInput();
            };
            
            // Also capture when window becomes visible
            IsVisibleChanged += (sender, e) =>
            {
                if (IsVisible)
                {
                    CaptureMouseInput();
                }
            };
            
            // Handle window closed event
            Closed += (sender, e) =>
            {
                if (_activeInstance == this)
                {
                    _activeInstance = null;
                }
            };
        }
        
        private void CaptureMouseInput()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // Make window topmost so it receives mouse messages even when browser has focus
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                
                // Capture all mouse input to this window
                // This ensures clicks go to our overlay even when browser has focus
                SetCapture(hwnd);
                System.Diagnostics.Debug.WriteLine("RegionSelectionWindow: Mouse capture set and window made topmost");
            }
        }
        
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Use Windows API to make this window truly non-focus-stealing
            // This prevents browser dropdowns from closing when region selection starts
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // Get current extended window style
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                
                // Add flags to prevent focus stealing
                // WS_EX_NOACTIVATE: Window won't activate when clicked (keeps browser focus)
                // WS_EX_TOOLWINDOW: Don't show in taskbar and don't activate
                // Note: We DON'T use WS_EX_TRANSPARENT because we need to capture mouse clicks
                extendedStyle |= WS_EX_NOACTIVATE;  // Window won't activate when clicked
                extendedStyle |= WS_EX_TOOLWINDOW;   // Don't show in taskbar and don't activate
                
                // Apply the new extended style
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
                
                System.Diagnostics.Debug.WriteLine("RegionSelectionWindow: Applied non-focus-stealing window style");
                
                // Capture mouse input immediately after window is initialized
                // This ensures mouse clicks go to our overlay, not the browser
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    CaptureMouseInput();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            // Ensure mouse capture is released when window closes
            ReleaseCapture();
            
            // Ensure magnifier is cleaned up when window closes
            StopMagnifier();
            base.OnClosed(e);
        }

        private Rectangle GetVirtualDesktopBounds()
        {
            var allScreens = System.Windows.Forms.Screen.AllScreens;
            if (allScreens.Length == 0)
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

            foreach (var screen in allScreens)
            {
                if (screen != null)
                {
                    minX = Math.Min(minX, screen.Bounds.X);
                    minY = Math.Min(minY, screen.Bounds.Y);
                    maxX = Math.Max(maxX, screen.Bounds.X + screen.Bounds.Width);
                    maxY = Math.Max(maxY, screen.Bounds.Y + screen.Bounds.Height);
                }
            }

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        private void PositionWindowForAllMonitors()
        {
            // Position the window to cover the entire virtual desktop
            Left = _virtualDesktopBounds.X;
            Top = _virtualDesktopBounds.Y;
            Width = _virtualDesktopBounds.Width;
            Height = _virtualDesktopBounds.Height;
        }

        private void InitializeMagnifier()
        {
            // Only initialize magnifier if the setting is enabled
            if (_settingsService?.CurrentSettings?.EnableMagnifier != true)
            {
                return;
            }
            
            try
            {
                var zoomLevel = _settingsService?.CurrentSettings?.MagnifierZoomLevel ?? 2.0;
                var mode = _settingsService?.CurrentSettings?.MagnifierMode ?? "Follow";
                var stationaryMonitor = _settingsService?.CurrentSettings?.MagnifierStationaryMonitor ?? "Primary Monitor";
                var stationaryX = _settingsService?.CurrentSettings?.MagnifierStationaryX ?? 100;
                var stationaryY = _settingsService?.CurrentSettings?.MagnifierStationaryY ?? 100;
                _magnifier = new MagnifierWindow(zoomLevel, mode, stationaryMonitor, stationaryX, stationaryY);
                
                // Create timer for updating magnifier
                _magnifierTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(50) // Update 20 times per second
                };
                _magnifierTimer.Tick += (sender, e) =>
                {
                    if (_magnifier != null)
                    {
                        _magnifier.UpdateMagnifier();
                    }
                };
                
                // Start magnifier immediately when window opens
                StartMagnifier();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize magnifier: {ex.Message}");
            }
        }
        
        private void StartMagnifier()
        {
            try
            {
                if (_magnifier != null && _magnifierTimer != null)
                {
                    _magnifier.ShowMagnifier();
                    _magnifierTimer.Start();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start magnifier: {ex.Message}");
            }
        }
        
        private void StopMagnifier()
        {
            try
            {
                if (_magnifierTimer != null)
                {
                    _magnifierTimer.Stop();
                }
                
                if (_magnifier != null)
                {
                    _magnifier.HideMagnifier();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to stop magnifier: {ex.Message}");
            }
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Intercept mouse down at window level before browser gets it
            e.Handled = true; // Prevent event from reaching browser
            
            // Capture mouse input immediately
            CaptureMouseInput();
            
            // Convert to canvas coordinates and handle
            var canvasPoint = e.GetPosition(SelectionCanvas);
            _startPoint = canvasPoint;
            _isSelecting = true;
            SelectionRect.Visibility = Visibility.Visible;
            
            System.Windows.Controls.Canvas.SetLeft(SelectionRect, _startPoint.X);
            System.Windows.Controls.Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }
        
        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Intercept mouse up at window level
            e.Handled = true; // Prevent event from reaching browser
            
            if (!_isSelecting) return;
            
            // Release mouse capture
            ReleaseCapture();
            
            _isSelecting = false;
            
            var endPoint = e.GetPosition(SelectionCanvas);
            
            var x = Math.Min(_startPoint.X, endPoint.X);
            var y = Math.Min(_startPoint.Y, endPoint.Y);
            var width = Math.Abs(endPoint.X - _startPoint.X);
            var height = Math.Abs(endPoint.Y - _startPoint.Y);
            
            if (width > 10 && height > 10)
            {
                // Convert window coordinates to screen coordinates
                var screenX = (int)(Left + x);
                var screenY = (int)(Top + y);
                
                SelectedRegion = new Rectangle(screenX, screenY, (int)width, (int)height);
                
                // If in recording mode, just close the window with the selected region
                // If in screenshot mode, capture the region and launch editor
                if (_isRecordingMode)
                {
                    // For recording, just close the window - the calling code will handle the recording
                    Close();
                }
                else
                {
                    // For screenshot capture, proceed with normal behavior
                    CaptureRegion();
                }
            }
            else
            {
                SelectionRect.Visibility = Visibility.Collapsed;
            }
        }
        
        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;
            
            // Intercept mouse move at window level
            e.Handled = true; // Prevent event from reaching browser
            
            var currentPoint = e.GetPosition(SelectionCanvas);
            
            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);
            
            System.Windows.Controls.Canvas.SetLeft(SelectionRect, x);
            System.Windows.Controls.Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;
        }
        
        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Mouse capture is already set when window loaded
            // Just ensure it's still active
            CaptureMouseInput();
            
            _startPoint = e.GetPosition(SelectionCanvas);
            _isSelecting = true;
            SelectionRect.Visibility = Visibility.Visible;
            
            System.Windows.Controls.Canvas.SetLeft(SelectionRect, _startPoint.X);
            System.Windows.Controls.Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            
            // Magnifier is already running since window opened
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            
            // Release mouse capture
            ReleaseCapture();
            
            _isSelecting = false;
            
            // Magnifier continues running until window closes
            
            var endPoint = e.GetPosition(SelectionCanvas);
            
            var x = Math.Min(_startPoint.X, endPoint.X);
            var y = Math.Min(_startPoint.Y, endPoint.Y);
            var width = Math.Abs(endPoint.X - _startPoint.X);
            var height = Math.Abs(endPoint.Y - _startPoint.Y);
            
            if (width > 10 && height > 10)
            {
                // Convert window coordinates to screen coordinates
                var screenX = (int)(Left + x);
                var screenY = (int)(Top + y);
                
                SelectedRegion = new Rectangle(screenX, screenY, (int)width, (int)height);
                
                // If in recording mode, just close the window with the selected region
                // If in screenshot mode, capture the region and launch editor
                if (_isRecordingMode)
                {
                    // For recording, just close the window - the calling code will handle the recording
                    Close();
                }
                else
                {
                    // For screenshot capture, proceed with normal behavior
                    CaptureRegion();
                }
            }
            else
            {
                SelectionRect.Visibility = Visibility.Collapsed;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;
            
            var currentPoint = e.GetPosition(SelectionCanvas);
            
            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);
            
            System.Windows.Controls.Canvas.SetLeft(SelectionRect, x);
            System.Windows.Controls.Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Key pressed in RegionSelectionWindow: {e.Key} (Handled: {e.Handled})");
            
            // Test any key press to see if keyboard input is working
            if (e.Key == Key.Space)
            {
                System.Diagnostics.Debug.WriteLine("SPACE key pressed - testing keyboard input");
                e.Handled = true;
                return;
            }
            
            if (e.Key == Key.Escape)
            {
                System.Diagnostics.Debug.WriteLine("ESC key pressed - canceling region selection");
                e.Handled = true; // Mark as handled to prevent other handlers from processing it
                
                // Reset the hotkey toggle state when ESC is pressed
                if (_activeInstance == this)
                {
                    _activeInstance = null;
                }
                
                // Notify that region selection was canceled
                OnRegionSelectionCanceled?.Invoke();
                
                // Magnifier will be stopped when window closes
                Close();
            }
        }

        private void CaptureRegion()
        {
            try
            {
                if (SelectedRegion.HasValue)
                {
                    // Stop magnifier before capturing
                    StopMagnifier();
                    
                    // Hide the selection UI before capturing
                    SelectionRect.Visibility = Visibility.Collapsed;
                    InstructionsText.Visibility = Visibility.Collapsed;
                    
                    // Hide the entire window to prevent it from appearing in the screenshot
                    Visibility = Visibility.Hidden;
                    
                    // Force UI update and wait a moment
                    Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                    System.Threading.Thread.Sleep(20); // Reduced delay for faster UI response
                    
                    // Use the selected region coordinates directly (they're already in screen coordinates)
                    var actualX = SelectedRegion.Value.X;
                    var actualY = SelectedRegion.Value.Y;
                    var actualWidth = SelectedRegion.Value.Width;
                    var actualHeight = SelectedRegion.Value.Height;
                    
                    // Capture the region but don't save yet
                    using var bitmap = new Bitmap(actualWidth, actualHeight);
                    using var graphics = Graphics.FromImage(bitmap);
                    
                    graphics.CopyFromScreen(actualX, actualY, 0, 0, new System.Drawing.Size(actualWidth, actualHeight));
                    
                    // Store the bitmap for later use - create a deep copy to avoid disposal issues
                    CapturedBitmap = new Bitmap(bitmap);
                    System.Diagnostics.Debug.WriteLine($"Captured region: {actualWidth}x{actualHeight} at ({actualX},{actualY})");
                    
                    // Launch the screenshot editor
                    LaunchEditor(CapturedBitmap);
                }
            }
            catch (Exception ex)
            {
                // Commented out false alarm - this can trigger when editor copy/save is successful
                // MessageBox.Show($"Failed to capture region: {ex.Message}", "Error", 
                //               MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Region capture exception (likely harmless): {ex.Message}");
                Close();
            }
        }

        private void LaunchEditor(Bitmap bitmap)
        {
            try
            {
                // Stop magnifier before launching editor
                StopMagnifier();
                
                // Hide this window
                Visibility = Visibility.Hidden;
                
                // Launch the screenshot editor
                var editor = new ScreenshotEditorWindow(bitmap, _screenshotService, _settingsService);
                
                // Make sure the editor window is visible and on top
                editor.WindowState = WindowState.Normal;
                editor.Visibility = Visibility.Visible;
                editor.Topmost = true;
                
                var result = editor.ShowDialog();
                
                // Update our captured bitmap with the edited result if available
                if (editor.FinalBitmap != null)
                {
                    CapturedBitmap?.Dispose();
                    CapturedBitmap = editor.FinalBitmap;
                }
                
                // Track if user completed an action in the editor
                EditorActionCompleted = editor.ImageSaved || editor.ImageCopied;
                EditorCopyRequested = editor.ImageCopied;
                EditorSaveRequested = editor.ImageSaved;
                
                // Close this window
                Close();
            }
            catch (Exception ex)
            {
                // Show error to user since editor is not working
                MessageBox.Show($"Failed to launch editor: {ex.Message}", "Editor Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Editor launch exception: {ex.Message}");
                Close();
            }
        }
    }
} 