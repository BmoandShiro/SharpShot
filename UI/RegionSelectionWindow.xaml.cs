using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SharpShot.Services;
using Point = System.Windows.Point;

namespace SharpShot.UI
{
    public partial class RegionSelectionWindow : Window
    {
        private readonly ScreenshotService _screenshotService;
        private readonly SettingsService? _settingsService;
        private Point _startPoint;
        private bool _isSelecting;
        public Rectangle? SelectedRegion { get; private set; }
        public Bitmap? CapturedBitmap { get; private set; }
        private Rectangle _virtualDesktopBounds;
        private MagnifierWindow? _magnifier;
        private System.Windows.Threading.DispatcherTimer? _magnifierTimer;

        public RegionSelectionWindow(ScreenshotService screenshotService, SettingsService? settingsService = null)
        {
            InitializeComponent();
            _screenshotService = screenshotService;
            _settingsService = settingsService;
            
            // Calculate virtual desktop bounds (all monitors combined)
            _virtualDesktopBounds = GetVirtualDesktopBounds();
            
            // Position and size the window to cover all monitors
            PositionWindowForAllMonitors();
            
            // Setup event handlers
            SelectionCanvas.MouseLeftButtonDown += OnMouseLeftButtonDown;
            SelectionCanvas.MouseLeftButtonUp += OnMouseLeftButtonUp;
            SelectionCanvas.MouseMove += OnMouseMove;
            KeyDown += OnKeyDown;
            
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
        }

        private Rectangle GetVirtualDesktopBounds()
        {
            var allScreens = System.Windows.Forms.Screen.AllScreens;
            if (allScreens.Length == 0)
            {
                // Fallback to primary screen if no screens detected
                return System.Windows.Forms.Screen.PrimaryScreen.Bounds;
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
            if (_settingsService?.CurrentSettings.EnableMagnifier != true)
            {
                return;
            }
            
            try
            {
                var zoomLevel = _settingsService?.CurrentSettings.MagnifierZoomLevel ?? 2.0;
                _magnifier = new MagnifierWindow(zoomLevel);
                
                // Create timer for updating magnifier
                _magnifierTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(50) // Update 20 times per second
                };
                _magnifierTimer.Tick += (sender, e) =>
                {
                    if (_magnifier != null && _isSelecting)
                    {
                        _magnifier.UpdateMagnifier();
                    }
                };
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

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(SelectionCanvas);
            _isSelecting = true;
            SelectionRect.Visibility = Visibility.Visible;
            
            System.Windows.Controls.Canvas.SetLeft(SelectionRect, _startPoint.X);
            System.Windows.Controls.Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            
            // Start magnifier when selection begins
            StartMagnifier();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            
            _isSelecting = false;
            
            // Stop magnifier when selection ends
            StopMagnifier();
            
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
                CaptureRegion();
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
            if (e.Key == Key.Escape)
            {
                // Stop magnifier when canceling
                StopMagnifier();
                Close();
            }
        }

        private void CaptureRegion()
        {
            try
            {
                if (SelectedRegion.HasValue)
                {
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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to capture region: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Clean up magnifier before closing
                StopMagnifier();
                Close();
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            // Ensure magnifier is cleaned up when window closes
            StopMagnifier();
            base.OnClosed(e);
        }
    }
} 