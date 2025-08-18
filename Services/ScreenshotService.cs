using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
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

        public void CopyToClipboard(Bitmap bitmap)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Starting clipboard copy for bitmap: {bitmap.Width}x{bitmap.Height}");
                
                // Log to file for debugging
                LogToFile($"Starting clipboard copy for bitmap: {bitmap.Width}x{bitmap.Height}");
                
                // Convert Bitmap to BitmapSource for WPF clipboard
                var bitmapSource = ConvertBitmapToBitmapSource(bitmap);
                
                // Use WPF clipboard instead of Windows Forms for better MSIX compatibility
                System.Windows.Clipboard.SetImage(bitmapSource);
                
                System.Diagnostics.Debug.WriteLine("Successfully set image to clipboard");
                LogToFile("Successfully set image to clipboard");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Copy to clipboard failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                LogToFile($"Copy to clipboard failed: {ex.Message}");
                LogToFile($"Exception type: {ex.GetType().Name}");
                throw; // Re-throw the exception so the calling code can handle it
            }
        }

        private System.Windows.Media.Imaging.BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;
            
            var bitmapImage = new System.Windows.Media.Imaging.BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = memory;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            
            return bitmapImage;
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