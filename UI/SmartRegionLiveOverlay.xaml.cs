using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SharpShot.Services;
using SharpShot.Utils;

namespace SharpShot.UI
{
    /// <summary>
    /// Always-on smart-region overlay while the dashboard toggle is enabled.
    /// Empty areas are click-through so you can switch windows; highlights are clickable for OCR.
    /// </summary>
    public partial class SmartRegionLiveOverlay : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;

        private readonly SettingsService _settingsService;
        private readonly List<Rectangle> _rects = new();
        private Rectangle _virtualDesktopBounds;
        private DispatcherTimer? _debounceTimer;
        private DispatcherTimer? _scrollDebounceTimer;
        private DispatcherTimer? _hoverPollTimer;
        private DispatcherTimer? _contentPollTimer;
        private IntPtr _pendingHwnd = IntPtr.Zero;
        private IntPtr _activeHwnd = IntPtr.Zero;
        private bool _busyOcr;
        private int _refreshGeneration;
        private int _lastContentFingerprint;
        private bool _enrichInFlight;

        public event Action? RequestRaiseDashboard;

        public SmartRegionLiveOverlay(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _virtualDesktopBounds = GetVirtualDesktopBounds();
            PositionForVirtualDesktop();
            Focusable = false;

            Loaded += (_, _) =>
            {
                LayoutHint();
                RefreshFromTracker();
                StartHoverPoll();
                StartContentPoll();
                RequestRaiseDashboard?.Invoke();
            };

            Closed += (_, _) =>
            {
                LastExternalWindowTracker.LastWindowChanged -= OnLastWindowChanged;
                LastExternalWindowTracker.ContentMayHaveChanged -= OnContentMayHaveChanged;
                _debounceTimer?.Stop();
                _scrollDebounceTimer?.Stop();
                _hoverPollTimer?.Stop();
                _contentPollTimer?.Stop();
            };
        }

        public void Start()
        {
            LastExternalWindowTracker.LastWindowChanged -= OnLastWindowChanged;
            LastExternalWindowTracker.LastWindowChanged += OnLastWindowChanged;
            LastExternalWindowTracker.ContentMayHaveChanged -= OnContentMayHaveChanged;
            LastExternalWindowTracker.ContentMayHaveChanged += OnContentMayHaveChanged;

            if (!IsVisible)
                Show();

            RefreshFromTracker();
            StartHoverPoll();
            StartContentPoll();
            RequestRaiseDashboard?.Invoke();
        }

        public void Stop()
        {
            LastExternalWindowTracker.LastWindowChanged -= OnLastWindowChanged;
            LastExternalWindowTracker.ContentMayHaveChanged -= OnContentMayHaveChanged;
            _debounceTimer?.Stop();
            _scrollDebounceTimer?.Stop();
            _hoverPollTimer?.Stop();
            _contentPollTimer?.Stop();
            Close();
        }

        private void StartHoverPoll()
        {
            _hoverPollTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _hoverPollTimer.Tick -= HoverPollTick;
            _hoverPollTimer.Tick += HoverPollTick;
            _hoverPollTimer.Start();
        }

        private void StartContentPoll()
        {
            // Catches scroll/keyboard navigation that doesn't raise CONTENTSCROLLED (Chromium).
            _contentPollTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _contentPollTimer.Tick -= ContentPollTick;
            _contentPollTimer.Tick += ContentPollTick;
            _contentPollTimer.Start();
        }

        private void HoverPollTick(object? sender, EventArgs e)
        {
            if (_busyOcr || !IsVisible) return;
            GetCursorPos(out POINT pt);
            UpdateHover(pt.X, pt.Y);
        }

        private void ContentPollTick(object? sender, EventArgs e)
        {
            if (_busyOcr || _enrichInFlight || !IsVisible || _activeHwnd == IntPtr.Zero)
                return;

            try
            {
                int fp = SmartRegionDetection.ComputeContentFingerprint(_activeHwnd);
                if (fp == 0 || fp == _lastContentFingerprint)
                    return;
                _lastContentFingerprint = fp;
                ScheduleScrollRefresh();
            }
            catch
            {
                // ignore transient capture failures
            }
        }

        private void OnLastWindowChanged(IntPtr hwnd)
        {
            _pendingHwnd = hwnd;
            Dispatcher.BeginInvoke(() =>
            {
                _debounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                _debounceTimer.Stop();
                _debounceTimer.Tick -= DebounceTick;
                _debounceTimer.Tick += DebounceTick;
                _debounceTimer.Start();
            });
        }

        private void OnContentMayHaveChanged()
        {
            Dispatcher.BeginInvoke(ScheduleScrollRefresh);
        }

        private void ScheduleScrollRefresh()
        {
            if (_busyOcr || !IsVisible) return;
            _scrollDebounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
            _scrollDebounceTimer.Stop();
            _scrollDebounceTimer.Tick -= ScrollDebounceTick;
            _scrollDebounceTimer.Tick += ScrollDebounceTick;
            _scrollDebounceTimer.Start();
        }

        private void ScrollDebounceTick(object? sender, EventArgs e)
        {
            _scrollDebounceTimer?.Stop();
            if (_activeHwnd == IntPtr.Zero)
                _activeHwnd = LastExternalWindowTracker.GetLastWindow();
            if (_activeHwnd != IntPtr.Zero)
                RefreshForWindow(_activeHwnd, forceOcr: true);
        }

        private void DebounceTick(object? sender, EventArgs e)
        {
            _debounceTimer?.Stop();
            RefreshForWindow(_pendingHwnd != IntPtr.Zero ? _pendingHwnd : LastExternalWindowTracker.GetLastWindow());
            RequestRaiseDashboard?.Invoke();
        }

        private void RefreshFromTracker()
        {
            var hwnd = LastExternalWindowTracker.GetLastWindow();
            if (hwnd == IntPtr.Zero)
                hwnd = SmartRegionDetection.ResolveTargetWindow();
            RefreshForWindow(hwnd);
        }

        private void RefreshForWindow(IntPtr hwnd, bool forceOcr = false)
        {
            if (_busyOcr) return;
            if (hwnd == IntPtr.Zero)
            {
                _activeHwnd = IntPtr.Zero;
                _rects.Clear();
                DrawHighlights();
                return;
            }

            _activeHwnd = hwnd;
            int gen = ++_refreshGeneration;

            try
            {
                if (!forceOcr)
                {
                    var quick = SmartRegionDetection.GetDetectedRegions(hwnd) ?? new List<Rectangle>();
                    _rects.Clear();
                    _rects.AddRange(quick);
                    DrawHighlights();
                }

                _ = EnrichWithOcrAsync(hwnd, gen);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SmartRegionLiveOverlay refresh: {ex.Message}");
            }
        }

        private async Task EnrichWithOcrAsync(IntPtr hwnd, int generation)
        {
            if (_enrichInFlight && generation < _refreshGeneration)
                return;

            _enrichInFlight = true;
            // Hide overlay so CopyFromScreen/PrintWindow fallbacks never OCR our own pink boxes
            var wasVisible = IsVisible;
            try
            {
                if (wasVisible)
                {
                    Visibility = Visibility.Hidden;
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                    await Task.Delay(16);
                }

                var enriched = await SmartRegionDetection.GetDetectedRegionsAsync(
                    hwnd,
                    denseOcr: _settingsService.CurrentSettings.UseDenseOcrForSmartRegions);
                if (generation != _refreshGeneration || _busyOcr || !IsLoaded || enriched == null)
                    return;

                _lastContentFingerprint = SmartRegionDetection.ComputeContentFingerprint(hwnd);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (generation != _refreshGeneration || _busyOcr || !IsLoaded) return;
                    _rects.Clear();
                    _rects.AddRange(enriched);
                    DrawHighlights();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SmartRegionLiveOverlay OCR enrich: {ex.Message}");
            }
            finally
            {
                if (wasVisible && IsLoaded)
                    Visibility = Visibility.Visible;
                if (generation == _refreshGeneration)
                    _enrichInFlight = false;
            }
        }

        private void DrawHighlights()
        {
            HighlightsCanvas.Children.Clear();
            var accent = TryFindResource("AccentBrush") as SolidColorBrush ?? new SolidColorBrush(Colors.Orange);

            foreach (var r in _rects)
            {
                if (!TryMapPhysicalRectToCanvas(r, out double x, out double y, out double w, out double h))
                    continue;

                var region = r; // copy for closure
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = w,
                    Height = h,
                    // Must be slightly filled to receive clicks; null fill is not hit-testable
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, accent.Color.R, accent.Color.G, accent.Color.B)),
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, accent.Color.R, accent.Color.G, accent.Color.B)),
                    StrokeThickness = 1.5,
                    Cursor = Cursors.Hand,
                    IsHitTestVisible = true,
                    Tag = region
                };
                System.Windows.Controls.Canvas.SetLeft(rect, x);
                System.Windows.Controls.Canvas.SetTop(rect, y);
                rect.MouseLeftButtonUp += async (_, e) =>
                {
                    e.Handled = true;
                    if (rect.Tag is Rectangle tagged)
                        await RunOcrOnRegionAsync(tagged);
                };
                HighlightsCanvas.Children.Add(rect);
            }
        }

        private void UpdateHover(int screenX, int screenY)
        {
            var best = SmartRegionDetection.GetSmallestRegionAtPoint(_rects, screenX, screenY);
            if (!best.HasValue || !TryMapPhysicalRectToCanvas(best.Value, out double x, out double y, out double w, out double h))
            {
                HoverRect.Visibility = Visibility.Collapsed;
                return;
            }

            var accent = TryFindResource("AccentBrush") as SolidColorBrush ?? new SolidColorBrush(Colors.Orange);
            HoverRect.Stroke = accent;
            HoverRect.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, accent.Color.R, accent.Color.G, accent.Color.B));
            HoverRect.Width = w;
            HoverRect.Height = h;
            System.Windows.Controls.Canvas.SetLeft(HoverRect, x);
            System.Windows.Controls.Canvas.SetTop(HoverRect, y);
            HoverRect.Visibility = Visibility.Visible;
        }

        private async Task RunOcrOnRegionAsync(Rectangle region)
        {
            if (_busyOcr) return;
            _busyOcr = true;
            Visibility = Visibility.Hidden;
            try
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                await Task.Delay(30);

                using var bmp = new Bitmap(Math.Max(1, region.Width), Math.Max(1, region.Height));
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(region.X, region.Y, 0, 0, region.Size);
                }

                if (!OcrService.IsAvailable())
                {
                    ThemedMessageBox.Show(
                        "OCR is not available. Make sure tessdata is installed next to SharpShot.",
                        "Smart Regions", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var words = await OcrService.RecognizeWordsAsync(bmp);
                string text = string.Join(" ", words.Select(w => w.Text).Where(t => !string.IsNullOrWhiteSpace(t))).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    ThemedMessageBox.Show("No text was recognized in that region.", "Smart Regions",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var ocrWindow = new OcrResultWindow(text)
                {
                    Topmost = true,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                ocrWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Smart region OCR: {ex.Message}");
            }
            finally
            {
                _busyOcr = false;
                if (IsLoaded)
                {
                    Visibility = Visibility.Visible;
                    RefreshFromTracker();
                    RequestRaiseDashboard?.Invoke();
                }
            }
        }

        private bool TryMapPhysicalRectToCanvas(Rectangle physical, out double x, out double y, out double w, out double h)
        {
            x = y = w = h = 0;
            double canvasW = RootCanvas.ActualWidth > 0 ? RootCanvas.ActualWidth : Width;
            double canvasH = RootCanvas.ActualHeight > 0 ? RootCanvas.ActualHeight : Height;
            if (canvasW <= 0 || canvasH <= 0 || _virtualDesktopBounds.Width <= 0 || _virtualDesktopBounds.Height <= 0)
                return false;

            double scaleX = canvasW / _virtualDesktopBounds.Width;
            double scaleY = canvasH / _virtualDesktopBounds.Height;
            x = (physical.X - _virtualDesktopBounds.X) * scaleX;
            y = (physical.Y - _virtualDesktopBounds.Y) * scaleY;
            w = physical.Width * scaleX;
            h = physical.Height * scaleY;
            return w > 1 && h > 1;
        }

        private void LayoutHint()
        {
            if (HintText == null) return;
            double canvasW = RootCanvas.ActualWidth > 0 ? RootCanvas.ActualWidth : Width;
            HintText.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            System.Windows.Controls.Canvas.SetLeft(HintText, Math.Max(0, (canvasW - HintText.DesiredSize.Width) / 2));
        }

        private void PositionForVirtualDesktop()
        {
            double dpi = 1.0;
            try
            {
                var hwnd = new WindowInteropHelper(this).EnsureHandle();
                uint d = GetDpiForWindow(hwnd);
                if (d > 0) dpi = d / 96.0;
            }
            catch { /* fall back */ }

            var b = _virtualDesktopBounds;
            Left = b.X / dpi;
            Top = b.Y / dpi;
            Width = b.Width / dpi;
            Height = b.Height / dpi;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

            var b = _virtualDesktopBounds;
            SetWindowPos(hwnd, HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, SWP_SHOWWINDOW | SWP_NOACTIVATE);
            PositionForVirtualDesktop();
            SetWindowPos(hwnd, HWND_TOPMOST, b.X, b.Y, b.Width, b.Height, SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }

        /// <summary>Called by MainWindow so the dashboard stays above this overlay.</summary>
        public void RelinquishTopMostToDashboard()
        {
            // Overlay stays Topmost=true in WPF; dashboard will SetWindowPos after us.
        }

        private static Rectangle GetVirtualDesktopBounds()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (screens.Length == 0)
                return System.Windows.Forms.Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var s in screens)
            {
                minX = Math.Min(minX, s.Bounds.X);
                minY = Math.Min(minY, s.Bounds.Y);
                maxX = Math.Max(maxX, s.Bounds.X + s.Bounds.Width);
                maxY = Math.Max(maxY, s.Bounds.Y + s.Bounds.Height);
            }
            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);
    }
}
