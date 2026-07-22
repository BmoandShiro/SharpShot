using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpShot.Services;

namespace SharpShot.UI
{
    public partial class MagnifierWindow : Window
    {
        private int _magnifierSize = 200; // Size of the magnifier window (can be changed via settings)
        private int _captureSize; // Size of area to capture around cursor (calculated based on zoom)
        private double _zoomLevel = 2.0; // Default zoom level
        private double _currentX, _currentY; // Track current magnifier position
        private bool _isStationary = false; // Whether magnifier is in stationary mode
        private string _stationaryMonitor = "Primary Monitor"; // Monitor for stationary mode
        private double _stationaryX = 100, _stationaryY = 100; // Stationary position
        private string _mode = "Follow"; // "Follow", "Stationary", "Auto"
        private List<string> _autoStationaryMonitors = new List<string>(); // Monitors that should use stationary in Auto mode
        private Services.SettingsService _settingsService; // Reference to settings service for dynamic updates
        
        // Cached monitor information for efficient boundary-based detection
        private class MonitorInfo
        {
            public System.Drawing.Rectangle Bounds;
            public double DpiScale;
            public bool IsPrimary;
        }
        private List<MonitorInfo> _monitorCache = new List<MonitorInfo>();
        private bool _monitorCacheInitialized = false;
        
        // Cache mapping of monitor handles to screen indices for reliable DPI-aware detection
        private Dictionary<IntPtr, int> _monitorHandleToIndex = new Dictionary<IntPtr, int>();
        private bool _monitorHandleCacheInitialized = false;
        
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        
        // SetWindowPos flags. We position/size the magnifier in true physical pixels so it
        // lands exactly where we want regardless of the target monitor's DPI scale.
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const int MONITOR_DEFAULTTOPRIMARY = 0x00000001;
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int Size;
            public System.Drawing.Rectangle Monitor;
            public System.Drawing.Rectangle WorkArea;
            public uint Flags;
        }
        private const int MDT_EFFECTIVE_DPI = 0;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
        
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        
        private const uint SRCCOPY = 0x00CC0020;

        /// <summary>
        /// Optional frozen desktop bitmap. When set, the magnifier samples this instead of
        /// live BitBlt so it stays correct while a full-screen freeze overlay is on top.
        /// </summary>
        private Bitmap? _freezeSource;
        private Rectangle _freezeBounds;
        private Rectangle? _cachedVirtualDesktopBounds;

        public void SetFreezeFrameSource(Bitmap? freezeFrame, Rectangle virtualDesktopBounds)
        {
            _freezeSource = freezeFrame;
            _freezeBounds = virtualDesktopBounds;
            if (virtualDesktopBounds.Width > 0 && virtualDesktopBounds.Height > 0)
                _cachedVirtualDesktopBounds = virtualDesktopBounds;
        }
        
        public MagnifierWindow(double zoomLevel = 2.0, string mode = "Follow", string stationaryMonitor = "Primary Monitor", double stationaryX = 100, double stationaryY = 100, List<string> autoStationaryMonitors = null, Services.SettingsService settingsService = null)
        {
            InitializeComponent();

            // Set zoom level
            _zoomLevel = Math.Max(0.5, Math.Min(10.0, zoomLevel)); // Clamp between 0.5x and 10x
            
            // Get magnifier size from settings if available
            // For follow cursor mode, use MagnifierFollowSize (max 200), for stationary use MagnifierSize
            if (settingsService?.CurrentSettings != null)
            {
                if (mode == "Stationary")
                {
                    _magnifierSize = settingsService.CurrentSettings.MagnifierSize;
                }
                else
                {
                    // Follow cursor or Auto mode: use follow size (capped at 200)
                    _magnifierSize = Math.Min(200, settingsService.CurrentSettings.MagnifierFollowSize);
                }
            }

            // Calculate capture size based on zoom level
            _captureSize = (int)(_magnifierSize / _zoomLevel);

            // Set window size
            Width = _magnifierSize;
            Height = _magnifierSize;
            
            // Update crosshair positions based on magnifier size
            UpdateCrosshair();

            // Set mode and stationary settings
            _mode = mode;
            _stationaryMonitor = stationaryMonitor;
            _stationaryX = stationaryX;
            _stationaryY = stationaryY;
            _isStationary = (mode == "Stationary");
            _autoStationaryMonitors = autoStationaryMonitors ?? new List<string>();
            _settingsService = settingsService;

            // Update zoom level text
            ZoomLevelText.Text = $"{_zoomLevel:F1}x";
            
            // Setup non-focus-stealing window properties
            SourceInitialized += MagnifierWindow_SourceInitialized;
            
            // Initialize monitor cache for efficient boundary-based detection
            InitializeMonitorCache();
        }
        
        private void InitializeMonitorCache()
        {
            _monitorCache.Clear();
            _monitorHandleToIndex.Clear();
            var allScreens = System.Windows.Forms.Screen.AllScreens;
            
            // Build cache in same order as Screen.AllScreens to ensure index matching
            for (int i = 0; i < allScreens.Length; i++)
            {
                var screen = allScreens[i];
                if (screen != null)
                {
                    var bounds = screen.Bounds;
                    
                    // Get monitor handle for this screen by checking a point in the center
                    // This creates a reliable mapping that works regardless of DPI scaling
                    POINT centerPoint = new POINT 
                    { 
                        X = bounds.X + bounds.Width / 2, 
                        Y = bounds.Y + bounds.Height / 2 
                    };
                    IntPtr hMonitor = MonitorFromPoint(centerPoint, MONITOR_DEFAULTTONEAREST);
                    
                    // Store the mapping
                    if (hMonitor != IntPtr.Zero)
                    {
                        _monitorHandleToIndex[hMonitor] = i;
                    }
                    
                    // Get DPI for this monitor
                    double dpiScale = GetDpiScaleForPosition(centerPoint.X, centerPoint.Y);
                    
                    _monitorCache.Add(new MonitorInfo
                    {
                        Bounds = bounds,
                        DpiScale = dpiScale,
                        IsPrimary = screen.Primary
                    });
                }
            }
            
            _monitorCacheInitialized = true;
            _monitorHandleCacheInitialized = true;
        }
        
        private void MagnifierWindow_SourceInitialized(object? sender, EventArgs e)
        {
            // Apply Windows API flags to prevent the magnifier window from stealing focus
            // This prevents browser dropdowns from closing when the magnifier is shown
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // Get current extended window style
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                
                // Add flags to prevent activation
                // WS_EX_NOACTIVATE: Window won't activate when shown/clicked
                // WS_EX_TOOLWINDOW: Don't show in taskbar and don't activate
                extendedStyle |= WS_EX_NOACTIVATE;
                extendedStyle |= WS_EX_TOOLWINDOW;
                
                // Apply the new extended style
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);
                
                System.Diagnostics.Debug.WriteLine("MagnifierWindow: Applied non-focus-stealing window style");
            }
        }
        
        public void SetMode(string mode, string stationaryMonitor = null, double? stationaryX = null, double? stationaryY = null)
        {
            _mode = mode;
            _isStationary = (mode == "Stationary");
            
            if (stationaryMonitor != null)
                _stationaryMonitor = stationaryMonitor;
            if (stationaryX.HasValue)
                _stationaryX = stationaryX.Value;
            if (stationaryY.HasValue)
                _stationaryY = stationaryY.Value;
            
            // Update magnifier size from settings if available
            // Use appropriate size based on mode
            if (_settingsService?.CurrentSettings != null)
            {
                if (mode == "Stationary")
                {
                    _magnifierSize = _settingsService.CurrentSettings.MagnifierSize;
                }
                else
                {
                    // Follow cursor or Auto mode: use follow size (capped at 200)
                    _magnifierSize = Math.Min(200, _settingsService.CurrentSettings.MagnifierFollowSize);
                }
                Width = _magnifierSize;
                Height = _magnifierSize;
                _captureSize = (int)(_magnifierSize / _zoomLevel);
                UpdateCrosshair();
            }
            
            // Reinitialize monitor cache when mode changes to ensure it's up to date
            if (mode == "Auto")
            {
                InitializeMonitorCache();
            }
        }
        
        public void SetAutoStationaryMonitors(List<string> monitors)
        {
            _autoStationaryMonitors = monitors ?? new List<string>();
        }
        
        public void UpdateMagnifier()
        {
            try
            {
                // Get current cursor position
                GetCursorPos(out POINT cursorPos);
                
                // Determine if we should use stationary mode
                bool useStationary = _isStationary;
                string? boundaryBoxMonitor = null; // Store which monitor the boundary box is on (declared outside if block for scope)
                bool foundBoundaryBox = false; // Track if we found a boundary box (declared outside if block for scope)
                
                if (_mode == "Auto")
                {
                    // Auto mode: use stationary if cursor is on a monitor that's in the auto-stationary list
                    // Read current monitor list from settings service if available (for dynamic updates)
                    List<string> currentAutoStationaryMonitors = _autoStationaryMonitors;
                    if (_settingsService?.CurrentSettings?.MagnifierAutoStationaryMonitors != null)
                    {
                        currentAutoStationaryMonitors = _settingsService.CurrentSettings.MagnifierAutoStationaryMonitors;
                    }
                    
                    // First check boundary boxes (for DPI-scaled monitors that don't detect correctly)
                    // Also check if boundary boxes are selected in the auto-stationary list
                    int monitorIndex = -1;
                    var boundaryBoxes = _settingsService?.CurrentSettings?.MagnifierBoundaryBoxes;
                    if (boundaryBoxes != null && boundaryBoxes.Count > 0)
                    {
                        // Check each boundary box - it should be enabled AND in the selected monitors list
                        foreach (var box in boundaryBoxes)
                        {
                            var boxId = $"BoundaryBox:{box.Name}";
                            // Check if this boundary box is selected in auto-stationary monitors
                            bool isSelected = currentAutoStationaryMonitors.Contains(boxId);
                            
                            // Also check the Enabled property for backward compatibility
                            if (!box.Enabled && !isSelected) continue;
                            
                            var boundary = box.Bounds;
                            // Check if cursor is within this boundary box
                            if (cursorPos.X >= boundary.X && 
                                cursorPos.X < boundary.X + boundary.Width &&
                                cursorPos.Y >= boundary.Y && 
                                cursorPos.Y < boundary.Y + boundary.Height)
                            {
                                // If cursor is in a selected boundary box, use stationary mode
                                useStationary = true;
                                foundBoundaryBox = true;
                                
                                // Find which monitor this boundary box is on
                                var allScreens = System.Windows.Forms.Screen.AllScreens;
                                var centerX = boundary.X + boundary.Width / 2;
                                var centerY = boundary.Y + boundary.Height / 2;
                                
                                for (int i = 0; i < allScreens.Length; i++)
                                {
                                    if (allScreens[i].Bounds.Contains(centerX, centerY))
                                    {
                                        boundaryBoxMonitor = $"Monitor {i + 1}";
                                        monitorIndex = i;
                                        break;
                                    }
                                }
                                
                                // If we couldn't find the monitor by center, try the cursor position
                                if (boundaryBoxMonitor == null)
                                {
                                    for (int i = 0; i < allScreens.Length; i++)
                                    {
                                        if (allScreens[i].Bounds.Contains(cursorPos.X, cursorPos.Y))
                                        {
                                            boundaryBoxMonitor = $"Monitor {i + 1}";
                                            monitorIndex = i;
                                            break;
                                        }
                                    }
                                }
                                
                                break;
                            }
                        }
                    }
                    
                    // If no manual boundary matched, use automatic detection
                    if (monitorIndex < 0)
                    {
                        try
                        {
                            var screenPoint = new System.Drawing.Point(cursorPos.X, cursorPos.Y);
                            var screen = System.Windows.Forms.Screen.FromPoint(screenPoint);
                            
                            if (screen != null)
                            {
                                // Find the index of this screen in Screen.AllScreens
                                var allScreens = System.Windows.Forms.Screen.AllScreens;
                                for (int i = 0; i < allScreens.Length; i++)
                                {
                                    if (allScreens[i] == screen)
                                    {
                                        monitorIndex = i;
                                        break;
                                    }
                                }
                                
                                // If direct reference comparison fails, match by bounds
                                if (monitorIndex < 0)
                                {
                                    for (int i = 0; i < allScreens.Length; i++)
                                    {
                                        var s = allScreens[i];
                                        if (s.Bounds.X == screen.Bounds.X &&
                                            s.Bounds.Y == screen.Bounds.Y &&
                                            s.Bounds.Width == screen.Bounds.Width &&
                                            s.Bounds.Height == screen.Bounds.Height)
                                        {
                                            monitorIndex = i;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // If Screen.FromPoint fails, fall back to boundary check
                        }
                        
                        // Fallback: if Screen.FromPoint fails, use boundary check
                        if (monitorIndex < 0)
                        {
                            var allScreens = System.Windows.Forms.Screen.AllScreens;
                            for (int i = 0; i < allScreens.Length; i++)
                            {
                                var screen = allScreens[i];
                                var bounds = screen.Bounds;
                                
                                // Check if cursor is within this monitor's boundaries
                                if (cursorPos.X >= bounds.X && 
                                    cursorPos.X < bounds.X + bounds.Width &&
                                    cursorPos.Y >= bounds.Y && 
                                    cursorPos.Y < bounds.Y + bounds.Height)
                                {
                                    monitorIndex = i;
                                    break;
                                }
                            }
                        }
                    }
                    
                    // Only check monitor-based stationary if we didn't find a boundary box
                    if (!foundBoundaryBox)
                    {
                        if (monitorIndex >= 0)
                        {
                            // Check if this monitor is in the auto-stationary list
                            // Monitor IDs are "Monitor 1", "Monitor 2", etc. (1-based)
                            string monitorId = $"Monitor {monitorIndex + 1}";
                            useStationary = currentAutoStationaryMonitors.Contains(monitorId);
                        }
                        else
                        {
                            // Fallback: if monitor not found, don't use stationary
                            useStationary = false;
                        }
                    }
                }
                
                // Check if magnifier size has changed and update if needed
                // Use different size based on mode: stationary uses MagnifierSize, follow/auto uses MagnifierFollowSize (max 200)
                if (_settingsService?.CurrentSettings != null)
                {
                    int newSize;
                    if (useStationary)
                    {
                        // Stationary mode: use full size setting
                        newSize = _settingsService.CurrentSettings.MagnifierSize;
                    }
                    else
                    {
                        // Follow cursor or auto (follow mode): use follow size (capped at 200)
                        newSize = Math.Min(200, _settingsService.CurrentSettings.MagnifierFollowSize);
                    }
                    
                    if (newSize != _magnifierSize)
                    {
                        _magnifierSize = newSize;
                        _captureSize = (int)(_magnifierSize / _zoomLevel);
                    }
                }
                
                // Position + size the magnifier (physical pixels, DPI-aware).
                // Sizing is handled inside the Position* methods via ApplyPhysicalPlacement.
                if (useStationary)
                {
                    // If we're in a boundary box, use the monitor that the boundary box is on
                    if (boundaryBoxMonitor != null)
                    {
                        PositionMagnifierStationary(boundaryBoxMonitor);
                    }
                    else
                    {
                        PositionMagnifierStationary();
                    }
                }
                else
                {
                    PositionMagnifier(cursorPos.X, cursorPos.Y);
                }
                
                // Calculate capture area around cursor (always capture around cursor, not magnifier position)
                int captureX = cursorPos.X - _captureSize / 2;
                int captureY = cursorPos.Y - _captureSize / 2;

                // Get virtual desktop bounds for proper boundary checking (cached — hot path)
                var virtualDesktopBounds = _cachedVirtualDesktopBounds ?? GetVirtualDesktopBounds();
                _cachedVirtualDesktopBounds = virtualDesktopBounds;
                
                // Ensure capture area is within virtual desktop bounds
                captureX = Math.Max(virtualDesktopBounds.X, captureX);
                captureY = Math.Max(virtualDesktopBounds.Y, captureY);
                
                // Ensure capture area doesn't exceed virtual desktop bounds
                if (captureX + _captureSize > virtualDesktopBounds.X + virtualDesktopBounds.Width)
                {
                    captureX = virtualDesktopBounds.X + virtualDesktopBounds.Width - _captureSize;
                }
                if (captureY + _captureSize > virtualDesktopBounds.Y + virtualDesktopBounds.Height)
                {
                    captureY = virtualDesktopBounds.Y + virtualDesktopBounds.Height - _captureSize;
                }

                // Prefer freeze-frame sampling when region select has locked the desktop
                // (live BitBlt would only see the overlay / miss open menus).
                Bitmap? capturedBitmap = CaptureFromFreezeFrame(captureX, captureY, _captureSize, _captureSize)
                    ?? CaptureScreenAreaExcludingMagnifier(captureX, captureY, _captureSize, _captureSize);
                
                if (capturedBitmap != null)
                {
                    var imageSource = ConvertBitmapToImageSource(capturedBitmap);
                    capturedBitmap.Dispose();

                    // Timer already runs on the UI thread — avoid nested Dispatcher.Invoke
                    if (MagnifiedImage != null)
                        MagnifiedImage.Source = imageSource;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Magnifier update failed: {ex.Message}");
            }
        }
        
        private Bitmap? CaptureFromFreezeFrame(int screenX, int screenY, int width, int height)
        {
            if (_freezeSource == null || width <= 0 || height <= 0)
                return null;
            if (_freezeBounds.Width <= 0 || _freezeBounds.Height <= 0)
                return null;

            try
            {
                int srcX = screenX - _freezeBounds.X;
                int srcY = screenY - _freezeBounds.Y;
                var result = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(result))
                {
                    g.Clear(System.Drawing.Color.FromArgb(255, 26, 26, 26));
                    var srcRect = Rectangle.Intersect(
                        new Rectangle(0, 0, _freezeSource.Width, _freezeSource.Height),
                        new Rectangle(srcX, srcY, width, height));
                    if (srcRect.Width > 0 && srcRect.Height > 0)
                    {
                        int dstX = srcRect.X - srcX;
                        int dstY = srcRect.Y - srcY;
                        g.DrawImage(
                            _freezeSource,
                            new Rectangle(dstX, dstY, srcRect.Width, srcRect.Height),
                            srcRect,
                            GraphicsUnit.Pixel);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Freeze magnifier sample failed: {ex.Message}");
                return null;
            }
        }

        private Bitmap CaptureScreenAreaExcludingMagnifier(int x, int y, int width, int height)
        {
            IntPtr hdcScreen = GetDC(IntPtr.Zero);
            IntPtr hdcMemory = CreateCompatibleDC(hdcScreen);
            IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
            IntPtr hOldBitmap = SelectObject(hdcMemory, hBitmap);
            
            try
            {
                // Copy screen area to memory DC
                BitBlt(hdcMemory, 0, 0, width, height, hdcScreen, x, y, SRCCOPY);
                
                // Create .NET Bitmap from the captured area
                var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    var hdcBitmap = graphics.GetHdc();
                    BitBlt(hdcBitmap, 0, 0, width, height, hdcMemory, 0, 0, SRCCOPY);
                    graphics.ReleaseHdc(hdcBitmap);
                }
                
                // Calculate magnifier bounds in physical screen pixels. Prefer the real
                // window rectangle (exact, includes the border at the current DPI); fall
                // back to the tracked position if the HWND isn't available yet.
                var winRect = GetPhysicalWindowRect();
                int magnifierLeft, magnifierTop, magnifierRight, magnifierBottom;
                if (winRect.HasValue)
                {
                    magnifierLeft = winRect.Value.Left;
                    magnifierTop = winRect.Value.Top;
                    magnifierRight = winRect.Value.Right;
                    magnifierBottom = winRect.Value.Bottom;
                }
                else
                {
                    magnifierLeft = (int)_currentX - 2;
                    magnifierTop = (int)_currentY - 2;
                    magnifierRight = magnifierLeft + _magnifierSize + 4;
                    magnifierBottom = magnifierTop + _magnifierSize + 4;
                }
                
                // Calculate intersection with capture area
                int intersectLeft = Math.Max(x, magnifierLeft);
                int intersectTop = Math.Max(y, magnifierTop);
                int intersectRight = Math.Min(x + width, magnifierRight);
                int intersectBottom = Math.Min(y + height, magnifierBottom);
                
                // If there's an intersection, fill it with a dark color to hide the magnifier
                if (intersectLeft < intersectRight && intersectTop < intersectBottom)
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        // Convert intersection coordinates to bitmap coordinates
                        int bitmapLeft = intersectLeft - x;
                        int bitmapTop = intersectTop - y;
                        int bitmapWidth = intersectRight - intersectLeft;
                        int bitmapHeight = intersectBottom - intersectTop;
                        
                        // Fill the intersection area with dark color
                        using (var brush = new SolidBrush(System.Drawing.Color.FromArgb(255, 26, 26, 26))) // Dark background color
                        {
                            graphics.FillRectangle(brush, bitmapLeft, bitmapTop, bitmapWidth, bitmapHeight);
                        }
                    }
                }
                
                return bitmap;
            }
            finally
            {
                SelectObject(hdcMemory, hOldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(hdcMemory);
                ReleaseDC(IntPtr.Zero, hdcScreen);
            }
        }
        
        private Bitmap CaptureScreenArea(int x, int y, int width, int height)
        {
            IntPtr hdcScreen = GetDC(IntPtr.Zero);
            IntPtr hdcMemory = CreateCompatibleDC(hdcScreen);
            IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
            IntPtr hOldBitmap = SelectObject(hdcMemory, hBitmap);
            
            try
            {
                // Copy screen area to memory DC
                BitBlt(hdcMemory, 0, 0, width, height, hdcScreen, x, y, SRCCOPY);
                
                // Create .NET Bitmap from the captured area
                var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    var hdcBitmap = graphics.GetHdc();
                    BitBlt(hdcBitmap, 0, 0, width, height, hdcMemory, 0, 0, SRCCOPY);
                    graphics.ReleaseHdc(hdcBitmap);
                }
                
                return bitmap;
            }
            finally
            {
                SelectObject(hdcMemory, hOldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(hdcMemory);
                ReleaseDC(IntPtr.Zero, hdcScreen);
            }
        }
        
        private ImageSource ConvertBitmapToImageSource(Bitmap bitmap)
        {
            var handle = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    handle,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(handle);
            }
        }
        
        private void PositionMagnifier(int cursorX, int cursorY)
        {
            // Window size is already set in UpdateMagnifier before this is called
            // Get virtual desktop bounds (all monitors combined) instead of just primary screen
            var virtualDesktopBounds = GetVirtualDesktopBounds();
            
            // Get the specific screen where the cursor is currently located
            var currentScreen = GetScreenAtPosition(cursorX, cursorY);
            
            var screenWidth = virtualDesktopBounds.Width;
            var screenHeight = virtualDesktopBounds.Height;
            
            // Debug logging for troubleshooting
            System.Diagnostics.Debug.WriteLine($"Positioning magnifier - Cursor: ({cursorX}, {cursorY})");
            System.Diagnostics.Debug.WriteLine($"Virtual Desktop: W={screenWidth}, H={screenHeight}");
            if (currentScreen != null)
            {
                System.Diagnostics.Debug.WriteLine($"Current Screen: X={currentScreen.Bounds.X}, Y={currentScreen.Bounds.Y}, W={currentScreen.Bounds.Width}, H={currentScreen.Bounds.Height}");
            }
            
            // Calculate position to place magnifier farther from cursor to avoid self-capture
            double magnifierX = cursorX + 50; // Increased offset to the right
            double magnifierY = cursorY - _magnifierSize / 2; // Center vertically
            
            System.Diagnostics.Debug.WriteLine($"Initial magnifier position: ({magnifierX}, {magnifierY})");
            
            // Improved edge detection: Check if magnifier would go off the current monitor's right edge
            if (currentScreen != null)
            {
                var monitorBounds = currentScreen.Bounds;
                var monitorRightEdge = monitorBounds.X + monitorBounds.Width;
                var monitorLeftEdge = monitorBounds.X;
                
                // If magnifier would go off the right edge of current monitor, try left side
                if (magnifierX + _magnifierSize > monitorRightEdge)
                {
                    magnifierX = cursorX - _magnifierSize - 50; // Place to the left with increased offset
                    System.Diagnostics.Debug.WriteLine($"Switched to left side: {magnifierX}");
                    
                    // Ensure left side is still within current monitor bounds
                    if (magnifierX < monitorLeftEdge)
                    {
                        // If left side is also off-screen, position at monitor edge with small margin
                        magnifierX = monitorLeftEdge + 10;
                        System.Diagnostics.Debug.WriteLine($"Adjusted X to monitor left edge: {magnifierX}");
                    }
                }
                
                // If magnifier would go off the left edge of current monitor, try right side
                if (magnifierX < monitorLeftEdge)
                {
                    magnifierX = cursorX + 50; // Place to the right
                    System.Diagnostics.Debug.WriteLine($"Switched to right side: {magnifierX}");
                    
                    // Ensure right side is still within current monitor bounds
                    if (magnifierX + _magnifierSize > monitorRightEdge)
                    {
                        // If right side is also off-screen, position at monitor edge with small margin
                        magnifierX = monitorRightEdge - _magnifierSize - 10;
                        System.Diagnostics.Debug.WriteLine($"Adjusted X to monitor right edge: {magnifierX}");
                    }
                }
            }
            else
            {
                // Fallback to virtual desktop bounds if current screen detection fails
                if (magnifierX + _magnifierSize > virtualDesktopBounds.X + virtualDesktopBounds.Width)
                {
                    magnifierX = cursorX - _magnifierSize - 50; // Place to the left instead with increased offset
                    System.Diagnostics.Debug.WriteLine($"Switched to left side: {magnifierX}");
                }
                
                // Also ensure left boundary is respected
                if (magnifierX < virtualDesktopBounds.X)
                {
                    magnifierX = virtualDesktopBounds.X;
                    System.Diagnostics.Debug.WriteLine($"Adjusted X to virtual desktop left: {magnifierX}");
                }
            }
            
            // Fix: Use virtual desktop bounds instead of hardcoded 0 for Y positioning
            // This allows the magnifier to move to monitors with negative Y coordinates (like top monitors)
            if (magnifierY < virtualDesktopBounds.Y)
            {
                magnifierY = virtualDesktopBounds.Y;
                System.Diagnostics.Debug.WriteLine($"Adjusted Y to virtual desktop top: {magnifierY}");
            }
            else if (magnifierY + _magnifierSize > virtualDesktopBounds.Y + virtualDesktopBounds.Height)
            {
                magnifierY = virtualDesktopBounds.Y + virtualDesktopBounds.Height - _magnifierSize;
                System.Diagnostics.Debug.WriteLine($"Adjusted Y to virtual desktop bottom: {magnifierY}");
            }
            
            // Keep the magnifier fully within the current monitor's physical bounds.
            // (All coordinates here are true physical pixels because the process is
            // Per-Monitor-V2 DPI aware, so monitor bounds and cursor share one space.)
            if (currentScreen != null)
            {
                var monitorBounds = currentScreen.Bounds;
                
                if (magnifierX < monitorBounds.X)
                {
                    magnifierX = monitorBounds.X + 10;
                }
                if (magnifierX + _magnifierSize > monitorBounds.X + monitorBounds.Width)
                {
                    magnifierX = monitorBounds.X + monitorBounds.Width - _magnifierSize - 10;
                }
                if (magnifierY < monitorBounds.Y)
                {
                    magnifierY = monitorBounds.Y + 10;
                }
                if (magnifierY + _magnifierSize > monitorBounds.Y + monitorBounds.Height)
                {
                    magnifierY = monitorBounds.Y + monitorBounds.Height - _magnifierSize - 10;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Final magnifier position: ({magnifierX}, {magnifierY})");
            
            // Position + size the window in physical pixels (handles per-monitor DPI).
            ApplyPhysicalPlacement(magnifierX, magnifierY);
        }

        private System.Drawing.Rectangle GetVirtualDesktopBounds()
        {
            var allScreens = System.Windows.Forms.Screen.AllScreens;
            if (allScreens.Length == 0)
            {
                // Fallback to primary screen if no screens detected
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    // Ultimate fallback - return a default rectangle
                    return new System.Drawing.Rectangle(0, 0, 1920, 1080);
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

            var virtualBounds = new System.Drawing.Rectangle(minX, minY, maxX - minX, maxY - minY);
            return virtualBounds;
        }
        
        public void ShowMagnifier()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(ShowMagnifier);
                return;
            }

            Show();
            UpdateCrosshair();
            // Don't call Activate() - this would steal focus and close browser dropdowns
        }
        
        public void HideMagnifier()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(HideMagnifier);
                return;
            }

            Hide();
        }

        private System.Windows.Forms.Screen? GetScreenAtPosition(int x, int y)
        {
            var allScreens = System.Windows.Forms.Screen.AllScreens;
            foreach (var screen in allScreens)
            {
                if (screen.Bounds.Contains(x, y))
                {
                    return screen;
                }
            }
            return null;
        }
        
        private void UpdateCrosshair()
        {
            // The crosshair is now defined in XAML using stretch-based layout
            // (a centered Grid with stretched lines and a centered dot), so it stays
            // correct at any window size / DPI without manual pixel recalculation.
        }
        
        private void PositionMagnifierStationary(string? overrideMonitor = null)
        {
            // Find the monitor specified for stationary mode (or use override if provided)
            string monitorToUse = overrideMonitor ?? _stationaryMonitor;
            System.Windows.Forms.Screen targetScreen = null;
            var allScreens = System.Windows.Forms.Screen.AllScreens;
            
            if (monitorToUse == "Primary Monitor" || string.IsNullOrEmpty(monitorToUse))
            {
                targetScreen = System.Windows.Forms.Screen.PrimaryScreen;
                System.Diagnostics.Debug.WriteLine($"PositionMagnifierStationary: Using Primary Monitor");
            }
            else
            {
                // Parse monitor name (format: "Monitor 1", "Monitor 2", "Monitor 1 (Primary)", etc.)
                // Extract the monitor number from the name
                int monitorIndex = -1;
                
                // Try to extract number from "Monitor X" or "Monitor X (Primary)"
                var match = System.Text.RegularExpressions.Regex.Match(monitorToUse, @"Monitor\s+(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int monitorNumber))
                {
                    // Monitor numbers in UI are 1-based, but array is 0-based
                    monitorIndex = monitorNumber - 1;
                    System.Diagnostics.Debug.WriteLine($"PositionMagnifierStationary: Parsed monitor number {monitorNumber} (index {monitorIndex}) from '{monitorToUse}'");
                }
                
                // Use the parsed index to get the correct screen
                if (monitorIndex >= 0 && monitorIndex < allScreens.Length)
                {
                    targetScreen = allScreens[monitorIndex];
                    System.Diagnostics.Debug.WriteLine($"PositionMagnifierStationary: Selected monitor at index {monitorIndex}: Bounds=({targetScreen.Bounds.X}, {targetScreen.Bounds.Y}, {targetScreen.Bounds.Width}x{targetScreen.Bounds.Height})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"PositionMagnifierStationary: Could not parse monitor name '{monitorToUse}' or index {monitorIndex} is out of range (have {allScreens.Length} screens)");
                }
            }
            
            // Fallback to primary screen if we couldn't find the target
            if (targetScreen == null)
            {
                if (allScreens.Length > 0)
                {
                    targetScreen = System.Windows.Forms.Screen.PrimaryScreen ?? allScreens[0];
                    System.Diagnostics.Debug.WriteLine($"PositionMagnifierStationary: Falling back to primary screen or first screen");
                }
            }
            
            if (targetScreen != null)
            {
                var bounds = targetScreen.Bounds;
                // Convert logical position to physical pixels
                // For now, treat _stationaryX/Y as percentage of screen (0-100)
                double physicalX = bounds.X + (bounds.Width * _stationaryX / 100.0);
                double physicalY = bounds.Y + (bounds.Height * _stationaryY / 100.0);
                
                // Ensure magnifier stays within screen bounds
                physicalX = Math.Max(bounds.X, Math.Min(bounds.X + bounds.Width - _magnifierSize, physicalX));
                physicalY = Math.Max(bounds.Y, Math.Min(bounds.Y + bounds.Height - _magnifierSize, physicalY));
                
                // Position + size the window in physical pixels (handles per-monitor DPI).
                ApplyPhysicalPlacement(physicalX, physicalY);
            }
        }
        
        /// <summary>
        /// Positions and sizes the magnifier window using true physical pixel coordinates
        /// via SetWindowPos, so it lands exactly on the target monitor regardless of that
        /// monitor's DPI scale. WPF's logical Width/Height are kept in sync (physical / dpi)
        /// so WPF layout does not fight the Win32 sizing.
        /// </summary>
        private void ApplyPhysicalPlacement(double physX, double physY)
        {
            int px = (int)Math.Round(physX);
            int py = (int)Math.Round(physY);
            
            // DPI scale of the monitor the magnifier will sit on (sampled at its center).
            double dpi = GetDpiScaleForPosition(px + _magnifierSize / 2, py + _magnifierSize / 2);
            if (dpi <= 0) dpi = 1.0;
            
            _currentX = px;
            _currentY = py;
            
            Dispatcher.Invoke(() =>
            {
                // WidthDip * dpi == physical px, so WPF's logical size matches the physical size.
                double dipSize = _magnifierSize / dpi;
                Width = dipSize;
                Height = dipSize;
                
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, IntPtr.Zero, px, py, _magnifierSize, _magnifierSize, SWP_NOZORDER | SWP_NOACTIVATE);
                }
                else
                {
                    // HWND not created yet (window not shown yet) - best-effort logical placement.
                    Left = px / dpi;
                    Top = py / dpi;
                }
            });
        }
        
        /// <summary>
        /// Returns the magnifier window's rectangle in true physical screen pixels
        /// (including the accent border), or null if the HWND is not available yet.
        /// </summary>
        private System.Drawing.Rectangle? GetPhysicalWindowRect()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out RECT r))
            {
                return new System.Drawing.Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            }
            return null;
        }
        
        private double GetDpiScaleForPosition(int x, int y)
        {
            try
            {
                // Get the monitor handle for the given position
                POINT pt = new POINT { X = x, Y = y };
                IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                
                if (hMonitor != IntPtr.Zero)
                {
                    // Get DPI for the monitor
                    uint dpiX, dpiY;
                    int result = GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                    
                    if (result == 0) // Success
                    {
                        // Convert DPI to scaling factor (96 DPI = 100% = 1.0, 216 DPI = 225% = 2.25)
                        return dpiX / 96.0;
                    }
                }
            }
            catch
            {
                // If DPI API fails, fall back to default
            }
            
            // Default to 1.0 (100% scaling) if we can't determine DPI
            return 1.0;
        }
    }
} 