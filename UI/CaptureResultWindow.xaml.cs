using System;
using System.Windows;
using SharpShot.Services;
using SharpShot.Models;

namespace SharpShot.UI
{
    public partial class CaptureResultWindow : Window
    {
        private readonly ScreenshotService _screenshotService;
        private readonly string _filePath;
        private readonly SettingsService? _settingsService;

        public CaptureResultWindow(ScreenshotService screenshotService, string filePath, SettingsService? settingsService = null)
        {
            InitializeComponent();
            _screenshotService = screenshotService;
            _filePath = filePath;
            _settingsService = settingsService;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if popups are disabled
                if (_settingsService?.CurrentSettings?.DisableAllPopups == true)
                {
                    // Log the save operation instead of showing popup
                    System.Diagnostics.Debug.WriteLine($"Screenshot saved (popup disabled): {_filePath}");
                }
                else
                {
                    // File is already saved, just show confirmation
                    var result = MessageBox.Show(
                        $"Screenshot saved to:\n{_filePath}\n\nWould you like to open the folder?",
                        "SharpShot",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        // Open the folder containing the file
                        var folderPath = System.IO.Path.GetDirectoryName(_filePath);
                        if (!string.IsNullOrEmpty(folderPath))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", folderPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Always show error messages regardless of popup setting
                MessageBox.Show($"Failed to save screenshot: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Close();
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _screenshotService.CopyToClipboard(_filePath);
                
                // Check if popups are disabled
                if (_settingsService?.CurrentSettings?.DisableAllPopups == true)
                {
                    // Log the copy operation instead of showing popup
                    System.Diagnostics.Debug.WriteLine($"Screenshot copied to clipboard (popup disabled)");
                }
                else
                {
                    MessageBox.Show("Screenshot copied to clipboard!", "SharpShot", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // Always show error messages regardless of popup setting
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Close();
            }
        }
    }
} 