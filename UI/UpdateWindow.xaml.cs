using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SharpShot.Services;
using SharpShot.Utils;

namespace SharpShot.UI
{
    public partial class UpdateWindow : Window
    {
        private readonly UpdateService _updateService;
        private readonly UpdateInfo _updateInfo;
        private bool _isUpdating = false;

        public UpdateWindow(UpdateService updateService, UpdateInfo updateInfo)
        {
            InitializeComponent();
            _updateService = updateService;
            _updateInfo = updateInfo;

            var current = _updateService.GetCurrentVersion();
            // Was: Version + ReleaseName (often both "1.2.9.3" / "v1.2.9.3" from GitHub — looked like wrong "current" version)
            VersionText.Text = $"You have v{current} · New release: v{updateInfo.Version}";
            ReleaseNotesText.Text = updateInfo.ReleaseNotes;

            ApplyThemedButtons();

            // Make window draggable
            MouseDown += (s, e) => { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove(); };
        }

        private void ApplyThemedButtons()
        {
            var s = SharpShot.App.SettingsService.CurrentSettings;
            var iconColor = string.IsNullOrEmpty(s.IconColor) ? "#FFFF8C00" : s.IconColor;
            var color = (Color)ColorConverter.ConvertFromString(iconColor);
            var brush = new SolidColorBrush(color);

            LaterButton.Style = ThemeButtonStyleHelper.CreateModernButtonStyle(color, s.HoverOpacity, s.DropShadowOpacity, 100, 35);
            if (LaterButton.Content is System.Windows.Controls.TextBlock laterTb)
                laterTb.Foreground = brush;

            UpdateButton.Style = ThemeButtonStyleHelper.CreateModernButtonStyle(color, s.HoverOpacity, s.DropShadowOpacity, 120, 35);
            if (UpdateButton.Content is System.Windows.Controls.TextBlock updateTb)
                updateTb.Foreground = brush;

            UpdateCloseButton.Style = ThemeButtonStyleHelper.CreateCloseButtonStyle(color, s.HoverOpacity, s.DropShadowOpacity);
            if (UpdateCloseButton.Content is System.Windows.Controls.TextBlock closeTb)
                closeTb.Foreground = brush;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isUpdating)
            {
                Close();
            }
        }

        private void LaterButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isUpdating)
            {
                Close();
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            UpdateButton.IsEnabled = false;
            LaterButton.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;

            var progress = new Progress<UpdateProgress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressStatusText.Text = p.Status;
                    ProgressBar.Value = p.Percentage;
                    ProgressPercentageText.Text = $"{p.Percentage}%";
                });
            });

            try
            {
                var success = await _updateService.DownloadAndApplyUpdateAsync(_updateInfo, progress);
                
                if (!success)
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = "Update failed. Please try downloading manually from GitHub.";
                        StatusText.Visibility = Visibility.Visible;
                        UpdateButton.IsEnabled = true;
                        LaterButton.IsEnabled = true;
                        _isUpdating = false;
                    });
                }
                // If successful, the app will shut down and the update script will restart it
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"Error: {ex.Message}";
                    StatusText.Visibility = Visibility.Visible;
                    UpdateButton.IsEnabled = true;
                    LaterButton.IsEnabled = true;
                    _isUpdating = false;
                });
            }
        }
    }
}

