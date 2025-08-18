using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SharpShot.UI
{
    public partial class MagnifierWindow : Window
    {
        private const int MagnifierSize = 200;
        private int _captureSize; // Size of area to capture around cursor (calculated based on zoom)
        private double _zoomLevel = 2.0; // Default zoom level
        private double _currentX, _currentY; // Track current magnifier position
        
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
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
        
                public MagnifierWindow(double zoomLevel = 2.0)
        {
            InitializeComponent();

            // Set zoom level
            _zoomLevel = Math.Max(0.5, Math.Min(10.0, zoomLevel)); // Clamp between 0.5x and 10x

            // Calculate capture size based on zoom level
            _captureSize = (int)(MagnifierSize / _zoomLevel);

            // Set window size
            Width = MagnifierSize;
            Height = MagnifierSize;

            // Update zoom level text
            ZoomLevelText.Text = $"{_zoomLevel:F1}x";
        }
        
        public void UpdateMagnifier()
        {
            try
            {
                // Get current cursor position
                GetCursorPos(out POINT cursorPos);
                
                // Position magnifier first to know where it will be
                PositionMagnifier(cursorPos.X, cursorPos.Y);
                
                // Calculate capture area around cursor
                int captureX = cursorPos.X - _captureSize / 2;
                int captureY = cursorPos.Y - _captureSize / 2;

                // Get virtual desktop bounds for proper boundary checking
                var virtualDesktopBounds = GetVirtualDesktopBounds();
                
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

                // Capture screen area, excluding the magnifier's own area
                var capturedBitmap = CaptureScreenAreaExcludingMagnifier(captureX, captureY, _captureSize, _captureSize);
                
                if (capturedBitmap != null)
                {
                    // Convert to WPF ImageSource
                    var imageSource = ConvertBitmapToImageSource(capturedBitmap);
                    
                    // Update the magnified image
                    Dispatcher.Invoke(() =>
                    {
                        MagnifiedImage.Source = imageSource;
                    });
                    
                    // Clean up
                    capturedBitmap.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Magnifier update failed: {ex.Message}");
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
                
                // Calculate magnifier bounds in screen coordinates (including border)
                int magnifierLeft = (int)_currentX - 2; // Include border
                int magnifierTop = (int)_currentY - 2; // Include border
                int magnifierRight = magnifierLeft + MagnifierSize + 4; // Include border
                int magnifierBottom = magnifierTop + MagnifierSize + 4; // Include border
                
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
            double magnifierY = cursorY - MagnifierSize / 2; // Center vertically
            
            System.Diagnostics.Debug.WriteLine($"Initial magnifier position: ({magnifierX}, {magnifierY})");
            
            // Ensure magnifier stays within virtual desktop bounds
            if (magnifierX + MagnifierSize > virtualDesktopBounds.X + virtualDesktopBounds.Width)
            {
                magnifierX = cursorX - MagnifierSize - 50; // Place to the left instead with increased offset
                System.Diagnostics.Debug.WriteLine($"Switched to left side: {magnifierX}");
            }
            
            // Also ensure left boundary is respected
            if (magnifierX < virtualDesktopBounds.X)
            {
                magnifierX = virtualDesktopBounds.X;
                System.Diagnostics.Debug.WriteLine($"Adjusted X to virtual desktop left: {magnifierX}");
            }
            
            // Fix: Use virtual desktop bounds instead of hardcoded 0 for Y positioning
            // This allows the magnifier to move to monitors with negative Y coordinates (like top monitors)
            if (magnifierY < virtualDesktopBounds.Y)
            {
                magnifierY = virtualDesktopBounds.Y;
                System.Diagnostics.Debug.WriteLine($"Adjusted Y to virtual desktop top: {magnifierY}");
            }
            else if (magnifierY + MagnifierSize > virtualDesktopBounds.Y + virtualDesktopBounds.Height)
            {
                magnifierY = virtualDesktopBounds.Y + virtualDesktopBounds.Height - MagnifierSize;
                System.Diagnostics.Debug.WriteLine($"Adjusted Y to virtual desktop bottom: {magnifierY}");
            }
            
            // Additional DPI-aware positioning for 4K monitors
            if (currentScreen != null)
            {
                // Check if this is a high-DPI monitor (4K, etc.)
                bool isHighDPI = currentScreen.Bounds.Width >= 3840 || currentScreen.Bounds.Height >= 2160;
                
                if (isHighDPI)
                {
                    System.Diagnostics.Debug.WriteLine($"High-DPI monitor detected: {currentScreen.Bounds.Width}x{currentScreen.Bounds.Height}");
                    
                    // For 4K monitors, we need to be more careful about positioning
                    // Ensure the magnifier doesn't get cut off by the monitor's actual bounds
                    var monitorBounds = currentScreen.Bounds;
                    
                    // Adjust position to stay within the current monitor's bounds
                    if (magnifierX < monitorBounds.X)
                    {
                        magnifierX = monitorBounds.X + 10; // Small margin from left edge
                        System.Diagnostics.Debug.WriteLine($"Adjusted X to monitor left edge: {magnifierX}");
                    }
                    if (magnifierX + MagnifierSize > monitorBounds.X + monitorBounds.Width)
                    {
                        magnifierX = monitorBounds.X + monitorBounds.Width - MagnifierSize - 10; // Small margin from right edge
                        System.Diagnostics.Debug.WriteLine($"Adjusted X to monitor right edge: {magnifierX}");
                    }
                    if (magnifierY < monitorBounds.Y)
                    {
                        magnifierY = monitorBounds.Y + 10; // Small margin from top edge
                        System.Diagnostics.Debug.WriteLine($"Adjusted Y to monitor top edge: {magnifierY}");
                    }
                    if (magnifierY + MagnifierSize > monitorBounds.Y + monitorBounds.Height)
                    {
                        magnifierY = monitorBounds.Y + monitorBounds.Height - MagnifierSize - 10; // Small margin from bottom edge
                        System.Diagnostics.Debug.WriteLine($"Adjusted Y to monitor bottom edge: {magnifierY}");
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Final magnifier position: ({magnifierX}, {magnifierY})");
            
            // Store current position for exclusion logic
            _currentX = magnifierX;
            _currentY = magnifierY;
            
            // Update window position
            Dispatcher.Invoke(() =>
            {
                // Ensure the window can be positioned at negative coordinates (for top monitors)
                // Windows sometimes has issues with this, so we need to be explicit
                Left = magnifierX;
                Top = magnifierY;
                
                // Force a window position update to ensure it actually moves
                // This is especially important for monitors with negative coordinates
                if (WindowState == WindowState.Normal)
                {
                    // Force window to update its position
                    UpdateLayout();
                }
            });
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
            
            // Debug information to help troubleshoot positioning issues
            System.Diagnostics.Debug.WriteLine($"Virtual Desktop Bounds: X={virtualBounds.X}, Y={virtualBounds.Y}, W={virtualBounds.Width}, H={virtualBounds.Height}");
            System.Diagnostics.Debug.WriteLine($"Monitor count: {allScreens.Length}");
            foreach (var screen in allScreens)
            {
                if (screen != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Monitor: X={screen.Bounds.X}, Y={screen.Bounds.Y}, W={screen.Bounds.Width}, H={screen.Bounds.Height}, Primary={screen.Primary}");
                }
            }
            
            return virtualBounds;
        }
        
        public void ShowMagnifier()
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                Activate();
            });
        }
        
        public void HideMagnifier()
        {
            Dispatcher.Invoke(() =>
            {
                Hide();
            });
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
    }
} 