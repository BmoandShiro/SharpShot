using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace SharpShot.UI
{
    public partial class BoundarySelectionWindow : Window
    {
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
        
        private System.Drawing.Point _startCursorPhysical;
        
        private const int MDT_EFFECTIVE_DPI = 0;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;
        
        private Point _startPoint;
        private bool _isSelecting;
        public Rectangle? SelectedBoundary { get; private set; }
        private System.Drawing.Rectangle _targetBounds;

        private bool _shouldAccept = false;

        public BoundarySelectionWindow(System.Drawing.Rectangle targetBounds, string monitorName)
        {
            InitializeComponent();
            _targetBounds = targetBounds;
            Title = "Draw Boundary Box";
            
            // Position window to cover the target area (all monitors or specific monitor).
            // Logical placement here; corrected to exact physical pixels in OnSourceInitialized.
            double dpi = GetDpiScaleForPoint(targetBounds.X + 1, targetBounds.Y + 1);
            if (dpi <= 0) dpi = 1.0;
            Left = targetBounds.X / dpi;
            Top = targetBounds.Y / dpi;
            Width = targetBounds.Width / dpi;
            Height = targetBounds.Height / dpi;
            
            // Setup event handlers
            SelectionCanvas.MouseLeftButtonDown += OnMouseLeftButtonDown;
            SelectionCanvas.MouseLeftButtonUp += OnMouseLeftButtonUp;
            SelectionCanvas.MouseMove += OnMouseMove;
            KeyDown += OnKeyDown;
            PreviewKeyDown += OnKeyDown;
            
            // Handle closing to set DialogResult safely
            Closing += OnWindowClosing;
            
            Focusable = true;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyPhysicalBounds();
        }

        /// <summary>
        /// Places/sizes the overlay to cover the target area using true physical pixels, and
        /// syncs WPF's logical bounds so PointToScreen stays accurate on any-DPI monitors.
        /// </summary>
        private void ApplyPhysicalBounds()
        {
            var b = _targetBounds; // physical pixels (process is Per-Monitor-V2 aware)
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
            ApplyPhysicalBounds();
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
                // Fall back below on older Windows.
            }
            return GetDpiScaleForPoint(_targetBounds.X + 1, _targetBounds.Y + 1);
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

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Set DialogResult when closing, only if window was shown as dialog
            if (SelectedBoundary.HasValue && _shouldAccept)
            {
                try
                {
                    DialogResult = true;
                }
                catch
                {
                    // Ignore - caller will check SelectedBoundary
                }
            }
            else if (!_shouldAccept)
            {
                try
                {
                    DialogResult = false;
                }
                catch
                {
                    // Ignore
                }
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(SelectionCanvas);
            GetCursorPos(out POINT startCursor);
            _startCursorPhysical = new System.Drawing.Point(startCursor.X, startCursor.Y);
            _isSelecting = true;
            SelectionRect.Visibility = Visibility.Visible;
            
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            
            _isSelecting = false;
            
            // Use true physical-pixel cursor positions so the stored boundary box matches the
            // screen exactly on any-DPI monitors (the magnifier later hit-tests these against the
            // physical cursor position), independent of the overlay's DPI/transform.
            GetCursorPos(out POINT endCursor);
            var screenX = Math.Min(_startCursorPhysical.X, endCursor.X);
            var screenY = Math.Min(_startCursorPhysical.Y, endCursor.Y);
            var width = Math.Abs(endCursor.X - _startCursorPhysical.X);
            var height = Math.Abs(endCursor.Y - _startCursorPhysical.Y);

            if (width > 10 && height > 10)
            {
                SelectedBoundary = new Rectangle(screenX, screenY, width, height);
                _shouldAccept = true;
                
                // Close the window - DialogResult will be set in Closing event
                Close();
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
            
            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                SelectedBoundary = null;
                _shouldAccept = false;
                
                // Close the window - DialogResult will be set in Closing event
                Close();
            }
        }
    }
}

