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
                var bounds = Screen.PrimaryScreen.Bounds;
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
                
                // Try clipboard operation with retry mechanism
                bool clipboardSet = false;
                int retryCount = 0;
                const int maxRetries = 3;
                
                while (!clipboardSet && retryCount < maxRetries)
                {
                    try
                    {
                        // Clear clipboard first to avoid conflicts
                        System.Windows.Forms.Clipboard.Clear();
                        System.Threading.Thread.Sleep(50); // Brief delay
                        
                        // Use the simple SetImage method which is much faster
                        System.Windows.Forms.Clipboard.SetImage(bitmap);
                        clipboardSet = true;
                        System.Diagnostics.Debug.WriteLine("Successfully set image to clipboard");
                        LogToFile("Successfully set image to clipboard");
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        System.Diagnostics.Debug.WriteLine($"Clipboard attempt {retryCount} failed: {ex.Message}");
                        LogToFile($"Clipboard attempt {retryCount} failed: {ex.Message}");
                        
                        if (retryCount < maxRetries)
                        {
                            System.Threading.Thread.Sleep(200); // Wait before retry
                        }
                        else
                        {
                            // If all retries failed, just throw the exception
                            throw;
                        }
                    }
                }
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

            overlay.Bounds = Screen.PrimaryScreen.Bounds;
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