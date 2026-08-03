using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using SharpShot.Models;

namespace SharpShot.Services
{
    public class ScreenshotService
    {
        private readonly SettingsService _settingsService;

        public ScreenshotService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public string CaptureFullScreen()
        {
            try
            {
                // Get bounds based on selected screen
                var bounds = GetBoundsForSelectedScreen();
                
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                
                return SaveScreenshot(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Full screen capture failed: {ex.Message}");
                return string.Empty;
            }
        }

        private Rectangle GetBoundsForSelectedScreen()
        {
            var selectedScreen = _settingsService.CurrentSettings.SelectedScreen;
            var allScreens = Screen.AllScreens;
            
            if (allScreens.Length == 0)
            {
                // Fallback to primary screen if no screens detected
                var primaryScreen = Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    // Ultimate fallback - return a default rectangle
                    return new Rectangle(0, 0, 1920, 1080);
                }
                return primaryScreen.Bounds;
            }
            
            // Handle different screen selection options
            switch (selectedScreen)
            {
                case "All Screens":
                case "All Monitors":
                    return GetVirtualDesktopBounds();
                    
                case "Primary Monitor":
                    var primaryScreen = Screen.PrimaryScreen;
                    if (primaryScreen == null)
                    {
                        // Ultimate fallback - return a default rectangle
                        return new Rectangle(0, 0, 1920, 1080);
                    }
                    return primaryScreen.Bounds;
                    
                default:
                    // Check if it's a specific monitor (e.g., "Monitor 1", "Monitor 2", etc.)
                    if (selectedScreen.StartsWith("Monitor "))
                    {
                        var monitorNumber = selectedScreen.Replace("Monitor ", "").Replace(" (Primary)", "");
                        if (int.TryParse(monitorNumber, out int index) && index > 0 && index <= allScreens.Length)
                        {
                            return allScreens[index - 1].Bounds;
                        }
                    }
                    
                    // Fallback to virtual desktop bounds
                    return GetVirtualDesktopBounds();
            }
        }

        private Rectangle GetVirtualDesktopBounds()
        {
            var allScreens = Screen.AllScreens;
            if (allScreens.Length == 0)
            {
                // Fallback to primary screen if no screens detected
                var primaryScreen = Screen.PrimaryScreen;
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
                minX = Math.Min(minX, screen.Bounds.X);
                minY = Math.Min(minY, screen.Bounds.Y);
                maxX = Math.Max(maxX, screen.Bounds.X + screen.Bounds.Width);
                maxY = Math.Max(maxY, screen.Bounds.Y + screen.Bounds.Height);
            }

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        public string CaptureRegion(Rectangle region)
        {
            try
            {
                using var bitmap = new Bitmap(region.Width, region.Height);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.CopyFromScreen(region.X, region.Y, 0, 0, region.Size);
                
                return SaveScreenshot(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Region capture failed: {ex.Message}");
                return string.Empty;
            }
        }

        public string SaveScreenshot(Bitmap bitmap)
        {
            var format = _settingsService.CurrentSettings.ScreenshotFormat.ToUpper();
            var imageFormat = format switch
            {
                "PNG" => ImageFormat.Png,
                "JPG" => ImageFormat.Jpeg,
                "JPEG" => ImageFormat.Jpeg,
                "BMP" => ImageFormat.Bmp,
                _ => ImageFormat.Png
            };

            var extension = format.ToLower();
            var fileName = $"SharpShot_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
            var savePath = Path.Combine(_settingsService.CurrentSettings.SavePath, fileName);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            bitmap.Save(savePath, imageFormat);
            return savePath;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        private const uint CF_DIB = 8;
        private const uint GMEM_MOVEABLE = 0x0002;
        private static readonly uint CF_PNG = RegisterClipboardFormat("PNG");

        public void CopyToClipboard(Bitmap bitmap)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Starting clipboard copy for bitmap: {bitmap.Width}x{bitmap.Height}");

                // #region agent log
                var swTotal = System.Diagnostics.Stopwatch.StartNew();
                // #endregion

                LogToFile($"Starting clipboard copy for bitmap: {bitmap.Width}x{bitmap.Height}");

                // Win32 clipboard with CF_DIB + PNG (apps like Discord need these; CF_BITMAP alone is not enough).
                // Fail-fast OpenClipboard retries — WPF Clipboard.SetImage blocks ~1s on CLIPBRD_E_CANT_OPEN.
                string lastError = "OpenClipboard Failed";
                int attempts = 0;
                bool ok = false;
                double setMs = 0;
                double prepMs = 0;

                // #region agent log
                var swPrep = System.Diagnostics.Stopwatch.StartNew();
                // #endregion
                // Build payloads before opening the clipboard so we hold the lock briefly.
                IntPtr hDib = CreateDibHGlobal(bitmap);
                IntPtr hPng = CreatePngHGlobal(bitmap);
                // #region agent log
                swPrep.Stop();
                prepMs = swPrep.Elapsed.TotalMilliseconds;
                // #endregion

                try
                {
                    for (attempts = 1; attempts <= 10; attempts++)
                    {
                        // #region agent log
                        var swAttempt = System.Diagnostics.Stopwatch.StartNew();
                        // #endregion

                        if (TrySetClipboardImageWin32(ref hDib, ref hPng, out lastError))
                        {
                            // #region agent log
                            swAttempt.Stop();
                            setMs = swAttempt.Elapsed.TotalMilliseconds;
                            ok = true;
                            // #endregion
                            break;
                        }

                        // #region agent log
                        swAttempt.Stop();
                        SharpShot.Utils.AgentDebugLog.Write("B", "ScreenshotService.CopyToClipboard", "win32 clipboard attempt failed",
                            new
                            {
                                attempt = attempts,
                                attemptMs = swAttempt.Elapsed.TotalMilliseconds,
                                error = lastError,
                                width = bitmap.Width,
                                height = bitmap.Height
                            }, runId: "post-fix");
                        // #endregion

                        try
                        {
                            System.Windows.Application.Current?.Dispatcher?.Invoke(
                                () => { },
                                System.Windows.Threading.DispatcherPriority.Background);
                        }
                        catch
                        {
                            // Ignore dispatcher pump failures
                        }
                    }
                }
                finally
                {
                    // If SetClipboardData never took ownership, free our prep buffers.
                    if (hDib != IntPtr.Zero)
                    {
                        GlobalFree(hDib);
                        hDib = IntPtr.Zero;
                    }
                    if (hPng != IntPtr.Zero)
                    {
                        GlobalFree(hPng);
                        hPng = IntPtr.Zero;
                    }
                }

                // #region agent log
                swTotal.Stop();
                SharpShot.Utils.AgentDebugLog.Write("B", "ScreenshotService.CopyToClipboard", ok ? "win32 clipboard success" : "win32 clipboard failed",
                    new
                    {
                        width = bitmap.Width,
                        height = bitmap.Height,
                        attempts,
                        prepMs,
                        setMs,
                        totalMs = swTotal.Elapsed.TotalMilliseconds,
                        formats = "CF_DIB+PNG",
                        error = ok ? null : lastError,
                        threadId = Environment.CurrentManagedThreadId
                    }, runId: "post-fix");
                // #endregion

                if (!ok)
                    throw new InvalidOperationException($"Copy to clipboard failed: {lastError}");

                System.Diagnostics.Debug.WriteLine("Successfully set image to clipboard");
                LogToFile("Successfully set image to clipboard");
            }
            catch (Exception ex)
            {
                // #region agent log
                SharpShot.Utils.AgentDebugLog.Write("B", "ScreenshotService.CopyToClipboard", "CopyToClipboard catch",
                    new { error = ex.GetType().Name, message = ex.Message }, runId: "post-fix");
                // #endregion
                System.Diagnostics.Debug.WriteLine($"Copy to clipboard failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                LogToFile($"Copy to clipboard failed: {ex.Message}");
                LogToFile($"Exception type: {ex.GetType().Name}");
                throw;
            }
        }

        /// <summary>
        /// Places CF_DIB + PNG on the clipboard. Returns false immediately if the clipboard is locked.
        /// On success, ownership of the HGLOBAL handles is transferred (refs set to Zero).
        /// </summary>
        private bool TrySetClipboardImageWin32(ref IntPtr hDib, ref IntPtr hPng, out string error)
        {
            error = string.Empty;
            try
            {
                if (!OpenClipboard(IntPtr.Zero))
                {
                    error = $"OpenClipboard Failed (0x{Marshal.GetLastWin32Error():X8})";
                    return false;
                }

                try
                {
                    EmptyClipboard();

                    if (hDib != IntPtr.Zero)
                    {
                        if (SetClipboardData(CF_DIB, hDib) == IntPtr.Zero)
                        {
                            error = $"SetClipboardData(CF_DIB) Failed (0x{Marshal.GetLastWin32Error():X8})";
                            return false;
                        }
                        hDib = IntPtr.Zero; // clipboard owns it
                    }

                    if (hPng != IntPtr.Zero && CF_PNG != 0)
                    {
                        if (SetClipboardData(CF_PNG, hPng) != IntPtr.Zero)
                            hPng = IntPtr.Zero;
                        // PNG is best-effort; CF_DIB alone covers most paste targets.
                    }

                    error = string.Empty;
                    return true;
                }
                finally
                {
                    CloseClipboard();
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static IntPtr CreateDibHGlobal(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Bmp);
            var bytes = ms.ToArray();
            // Strip BITMAPFILEHEADER (14 bytes) — clipboard CF_DIB wants BITMAPINFO + bits only.
            const int fileHeaderSize = 14;
            int dibSize = bytes.Length - fileHeaderSize;
            if (dibSize <= 0)
                throw new InvalidOperationException("Failed to create DIB for clipboard");

            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(uint)dibSize);
            if (hGlobal == IntPtr.Zero)
                throw new OutOfMemoryException("GlobalAlloc failed for CF_DIB");

            IntPtr locked = GlobalLock(hGlobal);
            if (locked == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                throw new InvalidOperationException("GlobalLock failed for CF_DIB");
            }

            try
            {
                Marshal.Copy(bytes, fileHeaderSize, locked, dibSize);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            return hGlobal;
        }

        private static IntPtr CreatePngHGlobal(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            var bytes = ms.ToArray();

            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(uint)bytes.Length);
            if (hGlobal == IntPtr.Zero)
                throw new OutOfMemoryException("GlobalAlloc failed for PNG");

            IntPtr locked = GlobalLock(hGlobal);
            if (locked == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                throw new InvalidOperationException("GlobalLock failed for PNG");
            }

            try
            {
                Marshal.Copy(bytes, 0, locked, bytes.Length);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            return hGlobal;
        }

        public void CopyToClipboard(string filePath)
        {
            try
            {
                using var bitmap = new Bitmap(filePath);
                CopyToClipboard(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Copy to clipboard failed: {ex.Message}");
                throw; // Re-throw the exception so the calling code can handle it
            }
        }

        public void ShowCaptureFeedback()
        {
            // Create a brief flash overlay
            var overlay = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                BackColor = Color.LightGray,
                Opacity = 0.3,
                TopMost = true,
                ShowInTaskbar = false
            };

            // Use bounds based on selected screen
            overlay.Bounds = GetBoundsForSelectedScreen();
            overlay.Show();

            var timer = new Timer { Interval = 200 };
            timer.Tick += (sender, e) =>
            {
                overlay.Close();
                timer.Dispose();
            };
            timer.Start();
        }

        private void LogToFile(string message)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sharpshot_debug.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
} 