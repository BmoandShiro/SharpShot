using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

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

                       // Ensure capture area is within screen bounds
                       captureX = Math.Max(0, captureX);
                       captureY = Math.Max(0, captureY);

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
            // Get screen dimensions
            var screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
            var screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
            
            // Calculate position to place magnifier farther from cursor to avoid self-capture
            double magnifierX = cursorX + 50; // Increased offset to the right
            double magnifierY = cursorY - MagnifierSize / 2; // Center vertically
            
            // Ensure magnifier stays within screen bounds
            if (magnifierX + MagnifierSize > screenWidth)
            {
                magnifierX = cursorX - MagnifierSize - 50; // Place to the left instead with increased offset
            }
            
            if (magnifierY < 0)
            {
                magnifierY = 0;
            }
            else if (magnifierY + MagnifierSize > screenHeight)
            {
                magnifierY = screenHeight - MagnifierSize;
            }
            
            // Store current position for exclusion logic
            _currentX = magnifierX;
            _currentY = magnifierY;
            
            // Update window position
            Dispatcher.Invoke(() =>
            {
                Left = magnifierX;
                Top = magnifierY;
            });
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
    }
} 