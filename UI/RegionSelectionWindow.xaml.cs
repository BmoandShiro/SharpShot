using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using SharpShot.Services;
using SharpShot.Utils;
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
        
        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        
        private const int MDT_EFFECTIVE_DPI = 0;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;
        
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOZORDER = 0x0004;
        
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
        private System.Drawing.Point _startCursorPhysical; // true physical-pixel cursor pos at drag start
        private bool _isSelecting;
        public Rectangle? SelectedRegion { get; private set; }
        public Bitmap? CapturedBitmap { get; private set; }
        public bool EditorActionCompleted { get; private set; } = false;
        public bool EditorCopyRequested { get; private set; } = false;
        public bool EditorSaveRequested { get; private set; } = false;
        public bool EditorRetakeRequested { get; private set; } = false;
        private Rectangle _virtualDesktopBounds;
        private MagnifierWindow? _magnifier;
        private System.Windows.Threading.DispatcherTimer? _magnifierTimer;
        private readonly bool _isRecordingMode;
        private readonly bool _directCaptureOnly;
        private readonly IntPtr _targetWindowForSmartDetection;
        private List<Rectangle> _smartRegionRects = new List<Rectangle>();
        private bool _isPotentialClick; // true until user moves enough to count as drag
        private Bitmap? _freezeFrame; // desktop snapshot taken before overlay (preserves menus)
        private Rectangle? _hoveredSmartRegion;

        public RegionSelectionWindow(ScreenshotService screenshotService, SettingsService? settingsService = null, bool isRecordingMode = false, IntPtr? targetWindowForSmartDetection = null, bool directCaptureOnly = false)
        {
            InitializeComponent();
            _screenshotService = screenshotService;
            _settingsService = settingsService;
            _isRecordingMode = isRecordingMode;
            _directCaptureOnly = directCaptureOnly;

            // Resolve target before we show anything; skip SharpShot windows (toolbar clicks).
            _targetWindowForSmartDetection = SmartRegionDetection.ResolveTargetWindow(
                targetWindowForSmartDetection ?? IntPtr.Zero);

            // Set this as the active instance
            _activeInstance = this;

            // Calculate virtual desktop bounds (all monitors combined)
            _virtualDesktopBounds = GetVirtualDesktopBounds();

            // Position and size the window to cover all monitors
            PositionWindowForAllMonitors();

            // CRITICAL: freeze the desktop BEFORE magnifier/overlay can steal focus and dismiss
            // open menus, dropdowns, or context menus. Selection crops from this bitmap later.
            CaptureFreezeFrame();

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
            SizeChanged += (_, _) =>
            {
                LayoutFreezeFrameLayers();
                if (IsLoaded)
                    DrawSmartRegionHighlights(_smartRegionRects);
            };

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

            if (_settingsService?.CurrentSettings?.EnableSmartRegionDetection == true)
            {
                InstructionsText.Text = "Click a highlighted region to copy its text (OCR), or drag to capture an image. Press ESC to cancel.";
            }

            // Magnifier is deferred until after the overlay paints — constructing/showing it in
            // the ctor/Loaded path is what causes the region-select / OCR click stutter.

            Loaded += (sender, e) =>
            {
                LayoutFreezeFrameLayers();
                DrawSmartRegionHighlights(_smartRegionRects);
                CaptureMouseInput();
                if (GetCursorPos(out POINT cursor))
                    UpdateSmartHover(cursor.X, cursor.Y);

                // Let the freeze overlay render a frame first, then bring up the magnifier.
                Dispatcher.BeginInvoke(new Action(StartMagnifierDeferred),
                    System.Windows.Threading.DispatcherPriority.ContextIdle);
            };

            IsVisibleChanged += (sender, e) =>
            {
                if (IsVisible)
                {
                    CaptureMouseInput();
                }
            };

            Closed += (sender, e) =>
            {
                if (_activeInstance == this)
                {
                    _activeInstance = null;
                }
                DisposeFreezeFrame();
            };
        }

        private void CaptureFreezeFrame()
        {
            try
            {
                DisposeFreezeFrame();
                var bounds = _virtualDesktopBounds;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                    return;

                // Do NOT hide SharpShot windows first — Visibility changes can dismiss menus.
                var bmp = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                }
                _freezeFrame = bmp;
                ApplyFreezeFrameToImage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Freeze frame capture failed: {ex.Message}");
                _freezeFrame = null;
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private void ApplyFreezeFrameToImage()
        {
            if (_freezeFrame == null || FreezeFrameImage == null)
                return;

            try
            {
                IntPtr hBitmap = _freezeFrame.GetHbitmap();
                try
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    FreezeFrameImage.Source = source;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Freeze frame image apply failed: {ex.Message}");
            }
        }

        private void LayoutFreezeFrameLayers()
        {
            if (SelectionCanvas == null) return;
            double w = SelectionCanvas.ActualWidth > 0 ? SelectionCanvas.ActualWidth : Width;
            double h = SelectionCanvas.ActualHeight > 0 ? SelectionCanvas.ActualHeight : Height;
            if (w <= 0 || h <= 0) return;

            if (FreezeFrameImage != null)
            {
                FreezeFrameImage.Width = w;
                FreezeFrameImage.Height = h;
                System.Windows.Controls.Canvas.SetLeft(FreezeFrameImage, 0);
                System.Windows.Controls.Canvas.SetTop(FreezeFrameImage, 0);
            }
            if (FreezeDimOverlay != null)
            {
                FreezeDimOverlay.Width = w;
                FreezeDimOverlay.Height = h;
                System.Windows.Controls.Canvas.SetLeft(FreezeDimOverlay, 0);
                System.Windows.Controls.Canvas.SetTop(FreezeDimOverlay, 0);
            }
        }

        private void DisposeFreezeFrame()
        {
            if (FreezeFrameImage != null)
                FreezeFrameImage.Source = null;
            _freezeFrame?.Dispose();
            _freezeFrame = null;
        }
        
        private void CaptureMouseInput()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // Topmost without activating — activation dismisses menus/dropdowns
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);

                // Capture all mouse input to this window
                SetCapture(hwnd);
                System.Diagnostics.Debug.WriteLine("RegionSelectionWindow: Mouse capture set and window made topmost (no-activate)");
            }

            // Region overlay re-asserts topmost; keep magnifier above it
            EnsureMagnifierOnTop();
        }

        private void EnsureMagnifierOnTop()
        {
            try
            {
                if (_magnifier == null || !_magnifier.IsVisible)
                    return;
                var hwnd = new WindowInteropHelper(_magnifier).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureMagnifierOnTop: {ex.Message}");
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
                
                // Now that the HWND exists, cover the whole virtual desktop using physical
                // pixels so the overlay lines up 1:1 with the screen regardless of per-monitor
                // DPI (needed for accurate PointToScreen coordinate mapping below).
                ApplyPhysicalDesktopBounds();
                
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
            // Initial logical placement (used before the HWND exists). Under Per-Monitor-V2
            // DPI awareness this is corrected precisely in OnSourceInitialized via
            // ApplyPhysicalDesktopBounds, which places the overlay using physical pixels.
            double dpi = GetDpiScaleForPoint(_virtualDesktopBounds.X + 1, _virtualDesktopBounds.Y + 1);
            if (dpi <= 0) dpi = 1.0;
            Left = _virtualDesktopBounds.X / dpi;
            Top = _virtualDesktopBounds.Y / dpi;
            Width = _virtualDesktopBounds.Width / dpi;
            Height = _virtualDesktopBounds.Height / dpi;
        }

        /// <summary>
        /// Places and sizes the overlay to cover the entire virtual desktop using true physical
        /// pixels (via SetWindowPos), then syncs WPF's logical Left/Top/Width/Height using the
        /// window's ACTUAL assigned DPI (GetDpiForWindow). Using the real window DPI - rather than
        /// a guessed per-monitor value - guarantees WPF's logical size matches the physical window
        /// rectangle on mixed-DPI desktops, so the overlay fully covers every monitor (no dead
        /// click zones) and PointToScreen / PointFromScreen stay accurate.
        /// </summary>
        private void ApplyPhysicalDesktopBounds()
        {
            var b = _virtualDesktopBounds; // physical pixels (process is Per-Monitor-V2 aware)
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            SetWindowPos(hwnd, HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, SWP_SHOWWINDOW | SWP_NOACTIVATE);

            double dpi = GetWindowDpiScale(hwnd);
            if (dpi <= 0) dpi = 1.0;

            Left = b.X / dpi;
            Top = b.Y / dpi;
            Width = b.Width / dpi;
            Height = b.Height / dpi;

            // Re-pin the physical rectangle in case assigning the WPF size/pos nudged the HWND.
            SetWindowPos(hwnd, HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            // Windows reassigned the window's DPI (e.g., its majority moved to another monitor);
            // re-apply so the overlay keeps covering the whole desktop at the new scale.
            ApplyPhysicalDesktopBounds();
        }

        private double GetWindowDpiScale(IntPtr hwnd)
        {
            try
            {
                uint dpi = GetDpiForWindow(hwnd);
                if (dpi > 0)
                    return dpi / 96.0;
            }
            catch
            {
                // GetDpiForWindow unavailable (pre-Win10 1607) - fall back below.
            }
            return GetDpiScaleForPoint(_virtualDesktopBounds.X + 1, _virtualDesktopBounds.Y + 1);
        }

        private double GetDpiScaleForPoint(int x, int y)
        {
            try
            {
                var pt = new POINT { X = x, Y = y };
                IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                if (hMonitor != IntPtr.Zero &&
                    GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0)
                {
                    return dpiX / 96.0;
                }
            }
            catch
            {
                // Fall through to default on any failure.
            }
            return 1.0;
        }

        private void InitializeMagnifier()
        {
            // Only initialize magnifier if the setting is enabled
            if (_settingsService?.CurrentSettings?.EnableMagnifier != true)
            {
                return;
            }

            if (_magnifier != null)
                return;
            
            try
            {
                var zoomLevel = _settingsService?.CurrentSettings?.MagnifierZoomLevel ?? 2.0;
                var mode = _settingsService?.CurrentSettings?.MagnifierMode ?? "Follow";
                var stationaryMonitor = _settingsService?.CurrentSettings?.MagnifierStationaryMonitor ?? "Primary Monitor";
                var stationaryX = _settingsService?.CurrentSettings?.MagnifierStationaryX ?? 100;
                var stationaryY = _settingsService?.CurrentSettings?.MagnifierStationaryY ?? 100;
                var autoStationaryMonitors = _settingsService?.CurrentSettings?.MagnifierAutoStationaryMonitors ?? new List<string>();
                _magnifier = new MagnifierWindow(zoomLevel, mode, stationaryMonitor, stationaryX, stationaryY, autoStationaryMonitors, _settingsService);
                if (_freezeFrame != null)
                    _magnifier.SetFreezeFrameSource(_freezeFrame, _virtualDesktopBounds);

                // Create timer for updating magnifier
                _magnifierTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(50) // Update 20 times per second
                };
                int tick = 0;
                _magnifierTimer.Tick += (sender, e) =>
                {
                    if (_magnifier == null) return;
                    _magnifier.UpdateMagnifier();
                    // Re-assert z-order occasionally — every tick causes visible hitching
                    if ((++tick & 7) == 0)
                        EnsureMagnifierOnTop();
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize magnifier: {ex.Message}");
            }
        }

        /// <summary>
        /// Create + show magnifier after the region overlay has painted.
        /// </summary>
        private void StartMagnifierDeferred()
        {
            if (_activeInstance != this)
                return;
            try
            {
                InitializeMagnifier();
                StartMagnifier();
                EnsureMagnifierOnTop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Deferred magnifier start failed: {ex.Message}");
            }
            finally
            {
                // After magnifier is up, walk UIA at idle so it doesn't hitch the open animation
                Dispatcher.BeginInvoke(new Action(StartSmartRegionDetectionIfEnabled),
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }
        
        private void StartMagnifier()
        {
            try
            {
                if (_magnifier != null && _magnifierTimer != null)
                {
                    _magnifier.ShowMagnifier();
                    // First content update immediately, then timer for follow
                    _magnifier.UpdateMagnifier();
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

        private void StartSmartRegionDetectionIfEnabled()
        {
            if (_isRecordingMode) return;
            if (_settingsService?.CurrentSettings?.EnableSmartRegionDetection != true) return;

            var target = _targetWindowForSmartDetection;
            if (target == IntPtr.Zero)
                target = SmartRegionDetection.ResolveTargetWindow();
            if (target == IntPtr.Zero) return;

            var hwnd = target;
            // UIA must run on the STA/UI thread. Caller schedules this at ApplicationIdle
            // so the freeze overlay + magnifier can appear first.
            if (_activeInstance != this)
                return;
            try
            {
                var rects = SmartRegionDetection.GetDetectedRegions(hwnd) ?? new List<Rectangle>();
                _smartRegionRects = rects;
                DrawSmartRegionHighlights(_smartRegionRects);
                if (GetCursorPos(out POINT cursor))
                    UpdateSmartHover(cursor.X, cursor.Y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Smart region detection: {ex.Message}");
            }
        }

        private void DrawSmartRegionHighlights(List<Rectangle> screenRects)
        {
            if (SmartHighlightsCanvas == null) return;
            SmartHighlightsCanvas.Children.Clear();
            if (screenRects.Count == 0) return;

            var accent = (SolidColorBrush)TryFindResource("AccentBrush") ?? new SolidColorBrush(Colors.Orange);
            // Subtle static outlines; hover uses SmartHoverRect for the active target
            foreach (var r in screenRects)
            {
                // Map physical screen pixels the same way the freeze-frame image is stretched
                // onto the canvas. PointFromScreen is wrong on mixed-DPI multi-monitor setups
                // because this overlay is a single HWND spanning all displays.
                if (!TryMapPhysicalRectToCanvas(r, out double x, out double y, out double w, out double h))
                    continue;

                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = w,
                    Height = h,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x55, accent.Color.R, accent.Color.G, accent.Color.B)),
                    StrokeThickness = 1
                };
                System.Windows.Controls.Canvas.SetLeft(rect, x);
                System.Windows.Controls.Canvas.SetTop(rect, y);
                SmartHighlightsCanvas.Children.Add(rect);
            }
        }

        private void UpdateSmartHover(int screenX, int screenY)
        {
            if (_settingsService?.CurrentSettings?.EnableSmartRegionDetection != true
                || _smartRegionRects.Count == 0
                || SmartHoverRect == null)
            {
                if (SmartHoverRect != null)
                    SmartHoverRect.Visibility = Visibility.Collapsed;
                _hoveredSmartRegion = null;
                return;
            }

            var best = SmartRegionDetection.GetSmallestRegionAtPoint(_smartRegionRects, screenX, screenY);
            _hoveredSmartRegion = best;
            if (!best.HasValue)
            {
                SmartHoverRect.Visibility = Visibility.Collapsed;
                return;
            }

            // Always apply current theme accent (StaticResource fill was hard-coded orange)
            var accent = TryFindResource("AccentBrush") as SolidColorBrush
                         ?? new SolidColorBrush(Colors.Orange);
            SmartHoverRect.Stroke = accent;
            SmartHoverRect.Fill = new SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x28, accent.Color.R, accent.Color.G, accent.Color.B));

            if (!TryMapPhysicalRectToCanvas(best.Value, out double x, out double y, out double w, out double h))
            {
                SmartHoverRect.Visibility = Visibility.Collapsed;
                return;
            }

            SmartHoverRect.Width = w;
            SmartHoverRect.Height = h;
            System.Windows.Controls.Canvas.SetLeft(SmartHoverRect, x);
            System.Windows.Controls.Canvas.SetTop(SmartHoverRect, y);
            SmartHoverRect.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Maps a physical-screen rectangle into SelectionCanvas DIPs using the same uniform
        /// scale as the freeze-frame image (Stretch=Fill over the virtual desktop).
        /// </summary>
        private bool TryMapPhysicalRectToCanvas(Rectangle physical, out double x, out double y, out double w, out double h)
        {
            x = y = w = h = 0;
            if (SelectionCanvas == null || _virtualDesktopBounds.Width <= 0 || _virtualDesktopBounds.Height <= 0)
                return false;

            double canvasW = SelectionCanvas.ActualWidth > 0 ? SelectionCanvas.ActualWidth : Width;
            double canvasH = SelectionCanvas.ActualHeight > 0 ? SelectionCanvas.ActualHeight : Height;
            if (canvasW <= 0 || canvasH <= 0)
                return false;

            double scaleX = canvasW / _virtualDesktopBounds.Width;
            double scaleY = canvasH / _virtualDesktopBounds.Height;

            x = (physical.X - _virtualDesktopBounds.X) * scaleX;
            y = (physical.Y - _virtualDesktopBounds.Y) * scaleY;
            w = physical.Width * scaleX;
            h = physical.Height * scaleY;

            if (w <= 1 || h <= 1)
                return false;
            if (x + w < 0 || y + h < 0 || x > canvasW || y > canvasH)
                return false;

            return true;
        }

        private Rectangle? GetSmartRegionAtScreenPoint(int screenX, int screenY)
        {
            return SmartRegionDetection.GetSmallestRegionAtPoint(_smartRegionRects, screenX, screenY);
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            CaptureMouseInput();
            var canvasPoint = e.GetPosition(SelectionCanvas);
            _startPoint = canvasPoint;
            GetCursorPos(out POINT startCursor);
            _startCursorPhysical = new System.Drawing.Point(startCursor.X, startCursor.Y);
            _isSelecting = true;
            _isPotentialClick = true;
            SelectionRect.Visibility = Visibility.Visible;
            System.Windows.Controls.Canvas.SetLeft(SelectionRect, _startPoint.X);
            System.Windows.Controls.Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }
        
        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (!_isSelecting) return;
            ReleaseCapture();
            _isSelecting = false;

            // Use the true physical-pixel cursor positions (captured at drag start + read now) so
            // the region matches the screen exactly, independent of the overlay's DPI/transform.
            GetCursorPos(out POINT endCursor);
            int startX = _startCursorPhysical.X;
            int startY = _startCursorPhysical.Y;
            var smartRect = GetSmartRegionAtScreenPoint(startX, startY);
            if (_isPotentialClick && smartRect.HasValue && _smartRegionRects.Count > 0)
            {
                SelectedRegion = smartRect;
                SelectionRect.Visibility = Visibility.Collapsed;
                if (_isRecordingMode)
                    Close();
                else if (_directCaptureOnly)
                    CaptureRegion();
                else
                    _ = CaptureSmartRegionAsOcrAsync();
                return;
            }

            var screenRectX = Math.Min(startX, endCursor.X);
            var screenRectY = Math.Min(startY, endCursor.Y);
            var screenWidth = Math.Abs(endCursor.X - startX);
            var screenHeight = Math.Abs(endCursor.Y - startY);
            if (screenWidth > 10 && screenHeight > 10)
            {
                SelectedRegion = new Rectangle(screenRectX, screenRectY, screenWidth, screenHeight);
                if (_isRecordingMode)
                    Close();
                else
                    CaptureRegion();
            }
            else
            {
                SelectionRect.Visibility = Visibility.Collapsed;
            }
        }
        
        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            e.Handled = true;
            GetCursorPos(out POINT cursor);
            if (!_isSelecting)
            {
                UpdateSmartHover(cursor.X, cursor.Y);
                return;
            }
            var currentPoint = e.GetPosition(SelectionCanvas);
            double dx = currentPoint.X - _startPoint.X;
            double dy = currentPoint.Y - _startPoint.Y;
            if (Math.Abs(dx) > 5 || Math.Abs(dy) > 5)
                _isPotentialClick = false;
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
            CaptureMouseInput();
            _startPoint = e.GetPosition(SelectionCanvas);
            GetCursorPos(out POINT startCursor);
            _startCursorPhysical = new System.Drawing.Point(startCursor.X, startCursor.Y);
            _isSelecting = true;
            _isPotentialClick = true;
            SelectionRect.Visibility = Visibility.Visible;
            System.Windows.Controls.Canvas.SetLeft(SelectionRect, _startPoint.X);
            System.Windows.Controls.Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            ReleaseCapture();
            _isSelecting = false;

            // Use true physical-pixel cursor positions (DPI/transform independent).
            GetCursorPos(out POINT endCursor);
            int startX = _startCursorPhysical.X;
            int startY = _startCursorPhysical.Y;
            var smartRect = GetSmartRegionAtScreenPoint(startX, startY);
            if (_isPotentialClick && smartRect.HasValue && _smartRegionRects.Count > 0)
            {
                SelectedRegion = smartRect;
                SelectionRect.Visibility = Visibility.Collapsed;
                if (_isRecordingMode) Close();
                else if (_directCaptureOnly) CaptureRegion();
                else _ = CaptureSmartRegionAsOcrAsync();
                return;
            }
            var screenRectX = Math.Min(startX, endCursor.X);
            var screenRectY = Math.Min(startY, endCursor.Y);
            var screenWidth = Math.Abs(endCursor.X - startX);
            var screenHeight = Math.Abs(endCursor.Y - startY);
            if (screenWidth > 10 && screenHeight > 10)
            {
                SelectedRegion = new Rectangle(screenRectX, screenRectY, screenWidth, screenHeight);
                if (_isRecordingMode) Close();
                else CaptureRegion();
            }
            else
            {
                SelectionRect.Visibility = Visibility.Collapsed;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            GetCursorPos(out POINT cursor);
            if (!_isSelecting)
            {
                UpdateSmartHover(cursor.X, cursor.Y);
                return;
            }
            var currentPoint = e.GetPosition(SelectionCanvas);
            double dx = currentPoint.X - _startPoint.X;
            double dy = currentPoint.Y - _startPoint.Y;
            if (Math.Abs(dx) > 5 || Math.Abs(dy) > 5)
                _isPotentialClick = false;
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

        private Bitmap? CropSelectedRegionFromFreezeOrScreen()
        {
            if (!SelectedRegion.HasValue)
                return null;

            var actualX = SelectedRegion.Value.X;
            var actualY = SelectedRegion.Value.Y;
            var actualWidth = SelectedRegion.Value.Width;
            var actualHeight = SelectedRegion.Value.Height;

            if (_freezeFrame != null)
            {
                var srcX = actualX - _virtualDesktopBounds.X;
                var srcY = actualY - _virtualDesktopBounds.Y;
                var cropRect = Rectangle.Intersect(
                    new Rectangle(0, 0, _freezeFrame.Width, _freezeFrame.Height),
                    new Rectangle(srcX, srcY, actualWidth, actualHeight));
                if (cropRect.Width > 0 && cropRect.Height > 0)
                    return _freezeFrame.Clone(cropRect, _freezeFrame.PixelFormat);
            }

            Visibility = Visibility.Hidden;
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            System.Threading.Thread.Sleep(20);

            using (CaptureUiSuppression.BeginIfEnabled(_settingsService))
            {
                using var bitmap = new Bitmap(actualWidth, actualHeight);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(actualX, actualY, 0, 0, new System.Drawing.Size(actualWidth, actualHeight));
                return new Bitmap(bitmap);
            }
        }

        /// <summary>
        /// Smart-region click: OCR the highlighted area and show copyable text instead of the image editor.
        /// </summary>
        private async Task CaptureSmartRegionAsOcrAsync()
        {
            try
            {
                StopMagnifier();
                SelectionRect.Visibility = Visibility.Collapsed;
                InstructionsText.Visibility = Visibility.Collapsed;
                if (SmartHoverRect != null)
                    SmartHoverRect.Visibility = Visibility.Collapsed;
                if (SmartHighlightsCanvas != null)
                    SmartHighlightsCanvas.Visibility = Visibility.Collapsed;

                var bitmap = CropSelectedRegionFromFreezeOrScreen();
                if (bitmap == null)
                {
                    Close();
                    return;
                }

                CapturedBitmap = bitmap;
                Visibility = Visibility.Hidden;

                if (!OcrService.IsAvailable())
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        ThemedMessageBox.Show(
                            "OCR is not available. Make sure tessdata (e.g. eng.traineddata) is installed next to SharpShot.",
                            "Smart Region OCR",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        LaunchEditor(CapturedBitmap);
                    });
                    return;
                }

                var words = await OcrService.RecognizeWordsAsync(bitmap);
                // Always resume on the UI thread for windows / message boxes
                await Dispatcher.InvokeAsync(() => { });

                string text = string.Join(" ", words.Select(w => w.Text).Where(t => !string.IsNullOrWhiteSpace(t))).Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    ThemedMessageBox.Show(
                        "No text was recognized in that region. Opening the image editor instead.",
                        "Smart Region OCR",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    LaunchEditor(CapturedBitmap);
                    return;
                }

                var ocrWindow = new OcrResultWindow(text)
                {
                    Topmost = true,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                ocrWindow.ShowDialog();

                EditorActionCompleted = true;
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Smart region OCR failed: {ex.Message}");
                try
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        ThemedMessageBox.Show(
                            $"OCR failed: {ex.Message}",
                            "Smart Region OCR",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        if (CapturedBitmap != null)
                            LaunchEditor(CapturedBitmap);
                        else
                            Close();
                    });
                }
                catch
                {
                    Close();
                }
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
                    if (SmartHoverRect != null)
                        SmartHoverRect.Visibility = Visibility.Collapsed;
                    if (SmartHighlightsCanvas != null)
                        SmartHighlightsCanvas.Visibility = Visibility.Collapsed;

                    CapturedBitmap = CropSelectedRegionFromFreezeOrScreen();
                    if (CapturedBitmap == null)
                    {
                        Close();
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"Captured region: {CapturedBitmap.Width}x{CapturedBitmap.Height}");

                    // For OCR quick-capture we only need a raw captured bitmap and should not show editor overlay.
                    if (_directCaptureOnly)
                    {
                        Close();
                    }
                    // Check if we should skip the editor and auto-copy
                    else if (_settingsService?.CurrentSettings?.SkipEditorAndAutoCopy == true)
                    {
                        // Skip editor, auto-copy to clipboard, and close
                        try
                        {
                            _screenshotService.CopyToClipboard(CapturedBitmap);
                            System.Diagnostics.Debug.WriteLine("Region screenshot copied to clipboard (editor skipped)");

                            // Mark that copy was requested
                            EditorCopyRequested = true;
                            EditorActionCompleted = true;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
                            // Still close the window even if copy fails
                        }

                        Close();
                    }
                    else
                    {
                        // Launch the screenshot editor (normal behavior)
                        LaunchEditor(CapturedBitmap);
                    }
                }
            }
            catch (Exception ex)
            {
                // Commented out false alarm - this can trigger when editor copy/save is successful
                // ThemedMessageBox.Show($"Failed to capture region: {ex.Message}", "Error",
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

                // Optionally move the editor to the monitor where the region was captured
                try
                {
                    if (_settingsService?.CurrentSettings?.EditorFollowsCaptureMonitor == true && SelectedRegion.HasValue)
                    {
                        var captureRect = SelectedRegion.Value;
                        var captureScreen = System.Windows.Forms.Screen.FromRectangle(captureRect);
                        editor.MoveToMonitorBounds(captureScreen.Bounds);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to move editor to capture monitor: {ex.Message}");
                }
                
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
                EditorRetakeRequested = editor.RetakeRequested;
                
                // Close this window
                Close();
            }
            catch (Exception ex)
            {
                // Show error to user since editor is not working
                ThemedMessageBox.Show($"Failed to launch editor: {ex.Message}", "Editor Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Editor launch exception: {ex.Message}");
                Close();
            }
        }
    }
} 