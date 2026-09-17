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

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_RETURN = 0x0D;
        private const int VK_SPACE = 0x20;
        private const int AdjustGripPhysical = 14;
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
        private bool _adjustMode;
        private bool _adjustDragActive;
        private bool _replacingAdjust;
        private bool _closingSelection;
        private AdjustHit _adjustHit;
        private Rectangle _adjustRectPhysical;
        private System.Drawing.Point _adjustDragOriginCursor;
        private Rectangle _adjustDragOriginRect;
        private readonly System.Windows.Shapes.Rectangle[] _adjustGrips = new System.Windows.Shapes.Rectangle[8];
        private IntPtr _kbHook;
        private LowLevelKeyboardProc? _kbHookProc;
        private volatile bool _keyHookActive;
        private Bitmap? _freezeFrame; // desktop snapshot taken before overlay (preserves menus)
        private Rectangle? _hoveredSmartRegion;
        private bool _applyingPhysicalBounds;

        public RegionSelectionWindow(ScreenshotService screenshotService, SettingsService? settingsService = null, bool isRecordingMode = false, IntPtr? targetWindowForSmartDetection = null, bool directCaptureOnly = false, Bitmap? preCapturedFreezeFrame = null)
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

            // Prefer a freeze frame captured off the UI thread by the caller. Fall back to
            // sync capture only if none was provided (keeps menus frozen before overlay show).
            if (preCapturedFreezeFrame != null)
                _freezeFrame = preCapturedFreezeFrame;
            else
                CaptureFreezeFrameGdiOnly();
            // Do NOT convert to BitmapSource here — that full-desktop HBITMAP→WPF path was the
            // open stutter. Apply after the window is loaded/shown.

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

            // Opaque window: freeze frame covers the full virtual desktop, so layered
            // transparency is unnecessary and made SetWindowPos(~5760x4452) take ~1.8s.
            WindowStyle = WindowStyle.None;
            Background = System.Windows.Media.Brushes.Black;

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
                if (_adjustMode)
                    SyncAdjustVisuals();
            };

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (sender, e) =>
            {
                if (_adjustMode)
                    return;
                InstructionsText.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();

            bool adjustBeforeEditor = settingsService?.CurrentSettings?.AdjustRegionBeforeEditor == true
                && !isRecordingMode && !directCaptureOnly;
            if (_settingsService?.CurrentSettings?.EnableSmartRegionDetection == true)
            {
                InstructionsText.Text = adjustBeforeEditor
                    ? "Click a highlight to copy text, or drag a region. Then resize the dashed box and press Enter. Esc cancels."
                    : "Click a highlighted region to copy its text (OCR), or drag to capture an image. Press ESC to cancel.";
            }
            else if (adjustBeforeEditor)
            {
                InstructionsText.Text = "Drag a region, then resize the dashed box. Enter to capture, Esc to cancel.";
            }

            // Magnifier is deferred until after the overlay paints — constructing/showing it in
            // the ctor/Loaded path is what causes the region-select / OCR click stutter.

            Loaded += (sender, e) =>
            {
                LayoutFreezeFrameLayers();
                // Convert GDI freeze → WPF image now that the window can paint (not in ctor).
                ApplyFreezeFrameToImage();
                DrawSmartRegionHighlights(_smartRegionRects);
                CaptureMouseInput();
                InstallAdjustKeyHook();
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
                RemoveAdjustKeyHook();
                if (_activeInstance == this)
                {
                    _activeInstance = null;
                }
                DisposeFreezeFrame();
            };
        }

        /// <summary>
        /// Captures the virtual desktop into a GDI bitmap on the calling thread.
        /// Prefer DXGI Desktop Duplication when enabled; fall back to GDI CopyFromScreen.
        /// Call from a background thread before constructing the overlay to avoid UI stutter.
        /// </summary>
        public static Bitmap? CreateFreezeFrameBitmap()
        {
            try
            {
                var bounds = GetVirtualDesktopBoundsStatic();
                if (bounds.Width <= 0 || bounds.Height <= 0)
                    return null;

                // Exclude SharpShot from the freeze frame. Visibility.Hidden is avoided when
                // WDA_EXCLUDEFROMCAPTURE works, because a visibility change dismisses menus.
                bool omitSharpShot = App.SettingsService?.CurrentSettings?.HideSharpShotWindowsDuringCapture == true;
                using (CaptureUiSuppression.BeginIfEnabled(App.SettingsService))
                {
                bool useDxgi = App.SettingsService?.CurrentSettings?.UseDxgiCapture == true;
                if (useDxgi)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var dxgiBmp = SharpShot.Utils.DxgiDesktopCapture.TryCaptureVirtualDesktop(out bounds, out string mode, omitSharpShot);
                    sw.Stop();
                    System.Diagnostics.Debug.WriteLine(
                        $"Freeze frame DXGI mode={mode}, size={dxgiBmp?.Width}x{dxgiBmp?.Height}, ms={sw.Elapsed.TotalMilliseconds:F1}");
                    if (dxgiBmp != null)
                        return dxgiBmp;
                }

                var gdiSw = System.Diagnostics.Stopwatch.StartNew();
                var bmp = SharpShot.Utils.DxgiDesktopCapture.CaptureVirtualDesktopGdi(bounds);
                gdiSw.Stop();
                System.Diagnostics.Debug.WriteLine(
                    $"Freeze frame GDI size={bmp.Width}x{bmp.Height}, ms={gdiSw.Elapsed.TotalMilliseconds:F1}");
                return bmp;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Freeze frame capture failed: {ex.Message}");
                return null;
            }
        }

        private void CaptureFreezeFrameGdiOnly()
        {
            try
            {
                DisposeFreezeFrame();
                _freezeFrame = CreateFreezeFrameBitmap();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Freeze frame capture failed: {ex.Message}");
                _freezeFrame = null;
            }
        }

        private void ApplyFreezeFrameToImage()
        {
            if (_freezeFrame == null || FreezeFrameImage == null)
                return;

            try
            {
                // LockBits → WriteableBitmap avoids GetHbitmap + CreateBitmapSourceFromHBitmap
                // (a second full-desktop pixel copy that stuttered on multi-monitor setups).
                var bmp = _freezeFrame;
                var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
                var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    var wb = new System.Windows.Media.Imaging.WriteableBitmap(
                        bmp.Width,
                        bmp.Height,
                        96,
                        96,
                        System.Windows.Media.PixelFormats.Bgra32,
                        null);
                    wb.WritePixels(
                        new Int32Rect(0, 0, bmp.Width, bmp.Height),
                        data.Scan0,
                        data.Stride * bmp.Height,
                        data.Stride);
                    wb.Freeze();
                    FreezeFrameImage.Source = wb;
                }
                finally
                {
                    bmp.UnlockBits(data);
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

        private Rectangle GetVirtualDesktopBounds() => GetVirtualDesktopBoundsStatic();

        private static Rectangle GetVirtualDesktopBoundsStatic()
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
            // SetWindowPos on a virtual-desktop-sized window dispatches nested WM_DPICHANGED /
            // layout messages. Without this guard, OnDpiChanged re-enters here and the open
            // path stalls for ~1–2s.
            if (_applyingPhysicalBounds)
                return;

            var b = _virtualDesktopBounds; // physical pixels (process is Per-Monitor-V2 aware)
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            _applyingPhysicalBounds = true;
            try
            {
                SetWindowPos(hwnd, HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, SWP_SHOWWINDOW | SWP_NOACTIVATE);

                double dpi = GetWindowDpiScale(hwnd);
                if (dpi <= 0) dpi = 1.0;

                Left = b.X / dpi;
                Top = b.Y / dpi;
                Width = b.Width / dpi;
                Height = b.Height / dpi;

                // Re-pin only if WPF size assignment moved the HWND off the physical rect.
                GetWindowRect(hwnd, out RECT wr);
                int w = wr.Right - wr.Left;
                int h = wr.Bottom - wr.Top;
                if (wr.Left != b.X || wr.Top != b.Y || w != b.Width || h != b.Height)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, SWP_SHOWWINDOW | SWP_NOACTIVATE);
                }
            }
            finally
            {
                _applyingPhysicalBounds = false;
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            // Ignore nested DPI changes caused by ApplyPhysicalDesktopBounds itself.
            if (_applyingPhysicalBounds)
                return;
            // Coalesce: one re-apply after the current DPI change settles.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_applyingPhysicalBounds || _activeInstance != this)
                    return;
                ApplyPhysicalDesktopBounds();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
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

                // Detect BEFORE starting the magnifier timer. ApplicationIdle never runs once the
                // 50ms Background-priority timer is pumping, which made smart regions vanish.
                StartSmartRegionDetectionIfEnabled();

                StartMagnifier();
                EnsureMagnifierOnTop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Deferred magnifier start failed: {ex.Message}");
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
            // UIA must run on the STA/UI thread. Caller schedules this after the freeze overlay paints.
            if (_activeInstance != this)
                return;
            try
            {
                // Fast UIA pass first so highlights appear immediately…
                var quick = SmartRegionDetection.GetDetectedRegions(hwnd) ?? new List<Rectangle>();
                _smartRegionRects = quick;
                DrawSmartRegionHighlights(_smartRegionRects);
                if (GetCursorPos(out POINT cursor))
                    UpdateSmartHover(cursor.X, cursor.Y);

                // …then OCR enrichment for browsers/consoles where UIA is sparse.
                _ = EnrichSmartRegionsWithOcrAsync(hwnd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Smart region detection: {ex.Message}");
            }
        }

        private async Task EnrichSmartRegionsWithOcrAsync(IntPtr hwnd)
        {
            try
            {
                var enriched = await SmartRegionDetection.GetDetectedRegionsAsync(
                    hwnd, _freezeFrame, _virtualDesktopBounds,
                    denseOcr: _settingsService?.CurrentSettings?.UseDenseOcrForSmartRegions ?? true,
                    onPartial: partial =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (_activeInstance != this || partial == null || partial.Count == 0)
                                return;
                            _smartRegionRects = partial;
                            DrawSmartRegionHighlights(_smartRegionRects);
                            if (GetCursorPos(out POINT cursor))
                                UpdateSmartHover(cursor.X, cursor.Y);
                        }));
                    });
                if (_activeInstance != this || enriched == null || enriched.Count == 0)
                    return;
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_activeInstance != this) return;
                    _smartRegionRects = enriched;
                    DrawSmartRegionHighlights(_smartRegionRects);
                    if (GetCursorPos(out POINT cursor))
                        UpdateSmartHover(cursor.X, cursor.Y);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Smart region OCR enrich: {ex.Message}");
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

        private enum AdjustHit { None, Move, N, S, E, W, NE, NW, SE, SW }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => HandlePointerDown(e, true);
        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => HandlePointerDown(e, false);
        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => HandlePointerUp(e, true);
        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => HandlePointerUp(e, false);
        private void OnPreviewMouseMove(object sender, MouseEventArgs e) => HandlePointerMove(e, true);
        private void OnMouseMove(object sender, MouseEventArgs e) => HandlePointerMove(e, false);

        private void HandlePointerDown(MouseButtonEventArgs e, bool markHandled)
        {
            if (markHandled) e.Handled = true;
            if (!GetCursorPos(out POINT cursor))
                return;

            if (_adjustMode && e.ClickCount >= 2 && HitTestAdjust(cursor.X, cursor.Y) != AdjustHit.None)
            {
                ConfirmAdjust();
                return;
            }

            if (_adjustMode)
            {
                var hit = HitTestAdjust(cursor.X, cursor.Y);
                if (hit != AdjustHit.None)
                {
                    CaptureMouseInput();
                    _adjustHit = hit;
                    _adjustDragActive = true;
                    _replacingAdjust = false;
                    _adjustDragOriginCursor = new System.Drawing.Point(cursor.X, cursor.Y);
                    _adjustDragOriginRect = _adjustRectPhysical;
                    _isSelecting = false;
                    return;
                }

                // Drag outside replaces the box. A click with no drag restores it.
                _replacingAdjust = true;
                HideAdjustChrome(keepMode: true);
            }

            CaptureMouseInput();
            _startPoint = e.GetPosition(SelectionCanvas);
            _startCursorPhysical = new System.Drawing.Point(cursor.X, cursor.Y);
            _isSelecting = true;
            _isPotentialClick = true;
            SelectionRect.Visibility = Visibility.Visible;
            System.Windows.Controls.Canvas.SetLeft(SelectionRect, _startPoint.X);
            System.Windows.Controls.Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }

        private void HandlePointerUp(MouseButtonEventArgs e, bool markHandled)
        {
            if (markHandled) e.Handled = true;

            if (_adjustDragActive)
            {
                ReleaseCapture();
                _adjustDragActive = false;
                var hit = _adjustHit;
                _adjustHit = AdjustHit.None;
                if (GetCursorPos(out POINT end))
                {
                    int moved = Math.Abs(end.X - _adjustDragOriginCursor.X) + Math.Abs(end.Y - _adjustDragOriginCursor.Y);
                    if (moved <= 6 && hit == AdjustHit.Move)
                    {
                        var smart = GetSmartRegionAtScreenPoint(end.X, end.Y);
                        if (smart.HasValue && _smartRegionRects.Count > 0 && !_isRecordingMode && !_directCaptureOnly)
                        {
                            ExitAdjustMode();
                            SelectedRegion = smart;
                            SelectionRect.Visibility = Visibility.Collapsed;
                            _ = CaptureSmartRegionAsOcrAsync();
                            return;
                        }
                    }
                }
                SyncAdjustVisuals();
                return;
            }

            if (!_isSelecting)
                return;
            ReleaseCapture();
            _isSelecting = false;
            if (!GetCursorPos(out POINT endCursor))
                return;

            int startX = _startCursorPhysical.X;
            int startY = _startCursorPhysical.Y;
            var smartRect = GetSmartRegionAtScreenPoint(startX, startY);
            if (_isPotentialClick && smartRect.HasValue && _smartRegionRects.Count > 0)
            {
                ExitAdjustMode();
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

            var screenWidth = Math.Abs(endCursor.X - startX);
            var screenHeight = Math.Abs(endCursor.Y - startY);
            if (screenWidth > 10 && screenHeight > 10)
            {
                var drawn = new Rectangle(
                    Math.Min(startX, endCursor.X),
                    Math.Min(startY, endCursor.Y),
                    screenWidth,
                    screenHeight);
                SelectedRegion = drawn;
                if (_isRecordingMode)
                    Close();
                else if (ShouldAdjustBeforeCommit())
                    EnterAdjustMode(drawn);
                else
                {
                    ExitAdjustMode();
                    CaptureRegion();
                }
            }
            else if (_replacingAdjust && _adjustMode)
            {
                _replacingAdjust = false;
                SyncAdjustVisuals();
            }
            else
            {
                SelectionRect.Visibility = Visibility.Collapsed;
            }
        }

        private void HandlePointerMove(MouseEventArgs e, bool markHandled)
        {
            if (markHandled) e.Handled = true;
            if (!GetCursorPos(out POINT cursor))
                return;

            if (_adjustDragActive)
            {
                ApplyAdjustDrag(cursor.X, cursor.Y);
                return;
            }

            if (_adjustMode && !_isSelecting)
            {
                UpdateAdjustCursor(HitTestAdjust(cursor.X, cursor.Y));
                UpdateSmartHover(cursor.X, cursor.Y);
                return;
            }

            if (!_isSelecting)
            {
                Cursor = Cursors.Arrow;
                UpdateSmartHover(cursor.X, cursor.Y);
                return;
            }

            var currentPoint = e.GetPosition(SelectionCanvas);
            if (Math.Abs(currentPoint.X - _startPoint.X) > 5 || Math.Abs(currentPoint.Y - _startPoint.Y) > 5)
                _isPotentialClick = false;
            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            System.Windows.Controls.Canvas.SetLeft(SelectionRect, x);
            System.Windows.Controls.Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = Math.Abs(currentPoint.X - _startPoint.X);
            SelectionRect.Height = Math.Abs(currentPoint.Y - _startPoint.Y);
        }

        private bool ShouldAdjustBeforeCommit()
        {
            if (_isRecordingMode || _directCaptureOnly)
                return false;
            return _settingsService?.CurrentSettings?.AdjustRegionBeforeEditor == true;
        }

        private void EnterAdjustMode(Rectangle physical)
        {
            _adjustMode = true;
            _replacingAdjust = false;
            _adjustHit = AdjustHit.None;
            _adjustDragActive = false;
            _adjustRectPhysical = physical;
            SelectedRegion = physical;
            _isSelecting = false;
            InstructionsText.Text = "Drag edges to adjust. Enter to capture, Esc to cancel.";
            InstructionsText.Visibility = Visibility.Visible;
            SyncAdjustVisuals();
        }

        private void ExitAdjustMode()
        {
            _adjustMode = false;
            _adjustDragActive = false;
            _replacingAdjust = false;
            _adjustHit = AdjustHit.None;
            HideAdjustChrome(keepMode: false);
            Cursor = Cursors.Arrow;
        }

        private void ConfirmAdjust()
        {
            if (!_adjustMode || _closingSelection)
                return;
            if (_adjustRectPhysical.Width < 10 || _adjustRectPhysical.Height < 10)
                return;
            SelectedRegion = _adjustRectPhysical;
            ExitAdjustMode();
            CaptureRegion();
        }

        private void CancelSelection()
        {
            if (_closingSelection)
                return;
            _closingSelection = true;
            RemoveAdjustKeyHook();
            if (_activeInstance == this)
                _activeInstance = null;
            OnRegionSelectionCanceled?.Invoke();
            Close();
        }

        private void ApplyAdjustDrag(int x, int y)
        {
            int dx = x - _adjustDragOriginCursor.X;
            int dy = y - _adjustDragOriginCursor.Y;
            var o = _adjustDragOriginRect;
            int left = o.Left, right = o.Right, top = o.Top, bottom = o.Bottom;
            switch (_adjustHit)
            {
                case AdjustHit.Move:
                    left += dx; right += dx; top += dy; bottom += dy;
                    break;
                case AdjustHit.N: top += dy; break;
                case AdjustHit.S: bottom += dy; break;
                case AdjustHit.W: left += dx; break;
                case AdjustHit.E: right += dx; break;
                case AdjustHit.NW: top += dy; left += dx; break;
                case AdjustHit.NE: top += dy; right += dx; break;
                case AdjustHit.SW: bottom += dy; left += dx; break;
                case AdjustHit.SE: bottom += dy; right += dx; break;
            }

            if (right < left) (left, right) = (right, left);
            if (bottom < top) (top, bottom) = (bottom, top);
            const int min = 10;
            if (right - left < min) right = left + min;
            if (bottom - top < min) bottom = top + min;

            var b = _virtualDesktopBounds;
            if (left < b.Left) { int shift = b.Left - left; left += shift; right += shift; }
            if (top < b.Top) { int shift = b.Top - top; top += shift; bottom += shift; }
            if (right > b.Right) { int shift = right - b.Right; left -= shift; right -= shift; }
            if (bottom > b.Bottom) { int shift = bottom - b.Bottom; top -= shift; bottom -= shift; }
            left = Math.Max(b.Left, left);
            top = Math.Max(b.Top, top);
            right = Math.Min(b.Right, Math.Max(left + min, right));
            bottom = Math.Min(b.Bottom, Math.Max(top + min, bottom));
            if (right - left < min || bottom - top < min)
                return;

            _adjustRectPhysical = new Rectangle(left, top, right - left, bottom - top);
            SelectedRegion = _adjustRectPhysical;
            SyncAdjustVisuals();
        }

        private AdjustHit HitTestAdjust(int x, int y)
        {
            var r = _adjustRectPhysical;
            int g = AdjustGripPhysical;
            bool nearL = Math.Abs(x - r.Left) <= g && y >= r.Top - g && y <= r.Bottom + g;
            bool nearR = Math.Abs(x - r.Right) <= g && y >= r.Top - g && y <= r.Bottom + g;
            bool nearT = Math.Abs(y - r.Top) <= g && x >= r.Left - g && x <= r.Right + g;
            bool nearB = Math.Abs(y - r.Bottom) <= g && x >= r.Left - g && x <= r.Right + g;
            if (nearL && nearT) return AdjustHit.NW;
            if (nearR && nearT) return AdjustHit.NE;
            if (nearL && nearB) return AdjustHit.SW;
            if (nearR && nearB) return AdjustHit.SE;
            if (nearT) return AdjustHit.N;
            if (nearB) return AdjustHit.S;
            if (nearL) return AdjustHit.W;
            if (nearR) return AdjustHit.E;
            if (x >= r.Left && x <= r.Right && y >= r.Top && y <= r.Bottom)
                return AdjustHit.Move;
            return AdjustHit.None;
        }

        private void UpdateAdjustCursor(AdjustHit hit)
        {
            Cursor = hit switch
            {
                AdjustHit.N or AdjustHit.S => Cursors.SizeNS,
                AdjustHit.E or AdjustHit.W => Cursors.SizeWE,
                AdjustHit.NE or AdjustHit.SW => Cursors.SizeNESW,
                AdjustHit.NW or AdjustHit.SE => Cursors.SizeNWSE,
                AdjustHit.Move => Cursors.SizeAll,
                _ => Cursors.Arrow
            };
        }

        private void SyncAdjustVisuals()
        {
            if (!_adjustMode)
                return;
            if (!TryMapPhysicalRectToCanvas(_adjustRectPhysical, out double x, out double y, out double w, out double h))
                return;

            SelectionRect.Visibility = Visibility.Visible;
            System.Windows.Controls.Canvas.SetLeft(SelectionRect, x);
            System.Windows.Controls.Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = w;
            SelectionRect.Height = h;
            LayoutAdjustGrips(x, y, w, h);
            LayoutDimHole(x, y, w, h);
            PositionAdjustInstructions();
        }

        private void LayoutAdjustGrips(double x, double y, double w, double h)
        {
            EnsureAdjustGrips();
            AdjustGripsCanvas.Visibility = Visibility.Visible;
            var points = new (double X, double Y)[]
            {
                (x, y), (x + w / 2, y), (x + w, y),
                (x + w, y + h / 2), (x + w, y + h), (x + w / 2, y + h),
                (x, y + h), (x, y + h / 2)
            };
            for (int i = 0; i < 8; i++)
            {
                System.Windows.Controls.Canvas.SetLeft(_adjustGrips[i], points[i].X - 4);
                System.Windows.Controls.Canvas.SetTop(_adjustGrips[i], points[i].Y - 4);
            }
        }

        private void EnsureAdjustGrips()
        {
            if (_adjustGrips[0] != null || AdjustGripsCanvas == null)
                return;
            var accent = TryFindResource("AccentBrush") as SolidColorBrush ?? new SolidColorBrush(Colors.Orange);
            for (int i = 0; i < 8; i++)
            {
                _adjustGrips[i] = new System.Windows.Shapes.Rectangle
                {
                    Width = 8,
                    Height = 8,
                    Fill = accent,
                    Stroke = System.Windows.Media.Brushes.White,
                    StrokeThickness = 1,
                    IsHitTestVisible = false
                };
                AdjustGripsCanvas.Children.Add(_adjustGrips[i]);
            }
        }

        private void LayoutDimHole(double x, double y, double w, double h)
        {
            double canvasW = SelectionCanvas.ActualWidth > 0 ? SelectionCanvas.ActualWidth : Width;
            double canvasH = SelectionCanvas.ActualHeight > 0 ? SelectionCanvas.ActualHeight : Height;
            FreezeDimOverlay.Visibility = Visibility.Collapsed;

            PlaceDimBand(DimBandTop, 0, 0, canvasW, Math.Max(0, y));
            PlaceDimBand(DimBandBottom, 0, y + h, canvasW, Math.Max(0, canvasH - (y + h)));
            PlaceDimBand(DimBandLeft, 0, y, Math.Max(0, x), Math.Max(0, h));
            PlaceDimBand(DimBandRight, x + w, y, Math.Max(0, canvasW - (x + w)), Math.Max(0, h));
        }

        private static void PlaceDimBand(System.Windows.Shapes.Rectangle band, double x, double y, double w, double h)
        {
            if (band == null) return;
            if (w <= 0 || h <= 0)
            {
                band.Visibility = Visibility.Collapsed;
                return;
            }
            System.Windows.Controls.Canvas.SetLeft(band, x);
            System.Windows.Controls.Canvas.SetTop(band, y);
            band.Width = w;
            band.Height = h;
            band.Visibility = Visibility.Visible;
        }

        private void HideAdjustChrome(bool keepMode)
        {
            if (!keepMode)
                _adjustMode = false;
            if (AdjustGripsCanvas != null)
                AdjustGripsCanvas.Visibility = Visibility.Collapsed;
            if (DimBandTop != null) DimBandTop.Visibility = Visibility.Collapsed;
            if (DimBandBottom != null) DimBandBottom.Visibility = Visibility.Collapsed;
            if (DimBandLeft != null) DimBandLeft.Visibility = Visibility.Collapsed;
            if (DimBandRight != null) DimBandRight.Visibility = Visibility.Collapsed;
            if (FreezeDimOverlay != null)
                FreezeDimOverlay.Visibility = Visibility.Visible;
        }

        private void PositionAdjustInstructions()
        {
            if (InstructionsText == null || SelectionCanvas == null)
                return;
            InstructionsText.Visibility = Visibility.Visible;
            double canvasW = SelectionCanvas.ActualWidth > 0 ? SelectionCanvas.ActualWidth : Width;
            InstructionsText.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            double textW = InstructionsText.DesiredSize.Width;
            System.Windows.Controls.Canvas.SetLeft(InstructionsText, Math.Max(12, (canvasW - textW) / 2));
            System.Windows.Controls.Canvas.SetTop(InstructionsText, 24);
        }

        private void InstallAdjustKeyHook()
        {
            _keyHookActive = true;
            if (_kbHook != IntPtr.Zero)
                return;
            _kbHookProc = AdjustKeyHook;
            _kbHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbHookProc, GetModuleHandle(null), 0);
        }

        private void RemoveAdjustKeyHook()
        {
            _keyHookActive = false;
            if (_kbHook == IntPtr.Zero)
                return;
            UnhookWindowsHookEx(_kbHook);
            _kbHook = IntPtr.Zero;
            _kbHookProc = null;
        }

        private IntPtr AdjustKeyHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (_keyHookActive && nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN) && lParam != IntPtr.Zero)
            {
                int vk = Marshal.ReadInt32(lParam);
                if (vk == VK_ESCAPE)
                {
                    Dispatcher.BeginInvoke(new Action(CancelSelection));
                    return (IntPtr)1;
                }
                if (_adjustMode && (vk == VK_RETURN || vk == VK_SPACE))
                {
                    Dispatcher.BeginInvoke(new Action(ConfirmAdjust));
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_kbHook, nCode, wParam, lParam);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CancelSelection();
                return;
            }

            if (_adjustMode && (e.Key == Key.Enter || e.Key == Key.Space))
            {
                e.Handled = true;
                ConfirmAdjust();
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
                    return SharpShot.Utils.DxgiDesktopCapture.CloneOpaque(_freezeFrame, cropRect);
            }

            Visibility = Visibility.Hidden;
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            System.Threading.Thread.Sleep(20);

            using (CaptureUiSuppression.BeginIfEnabled(_settingsService))
            {
                using var bitmap = new Bitmap(actualWidth, actualHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(actualX, actualY, 0, 0, new System.Drawing.Size(actualWidth, actualHeight));
                var result = new Bitmap(bitmap);
                SharpShot.Utils.DxgiDesktopCapture.EnsureOpaqueAlpha(result);
                return result;
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
                _keyHookActive = false;
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
                    HideAdjustChrome(keepMode: false);

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
                        // Skip editor — MainWindow performs the single clipboard copy
                        // (avoids double PNG/clipboard work that caused mouse hitch)
                        EditorCopyRequested = true;
                        EditorActionCompleted = true;
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
                _keyHookActive = false;

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