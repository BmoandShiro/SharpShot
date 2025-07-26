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
                // Create a memory stream for the image
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;

                // Create data object with multiple formats for better compatibility
                var dataObject = new System.Windows.Forms.DataObject();
                
                // Add the bitmap directly
                dataObject.SetData(System.Windows.Forms.DataFormats.Bitmap, bitmap);
                
                // Add as image stream
                dataObject.SetData("image/png", stream);
                
                // Add as file drop (some apps prefer this)
                var tempFile = Path.GetTempFileName() + ".png";
                bitmap.Save(tempFile, ImageFormat.Png);
                dataObject.SetData(System.Windows.Forms.DataFormats.FileDrop, new string[] { tempFile });

                // Set the data to clipboard
                System.Windows.Forms.Clipboard.SetDataObject(dataObject, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Copy to clipboard failed: {ex.Message}");
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
    }
} 