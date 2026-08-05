using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using SharpShot.Services;
using SharpShot.Utils;

namespace SharpShot.UI
{
    public partial class UpdateWindow : Window
    {
        private readonly UpdateService _updateService;
        private readonly UpdateInfo _updateInfo;
        private readonly bool _autoStartUpdate;
        private bool _isUpdating = false;

        /// <param name="autoStartUpdate">
        /// When true (Settings → Check Now), download/install starts immediately.
        /// When false (startup notification), the window is informational only.
        /// </param>
        public UpdateWindow(UpdateService updateService, UpdateInfo updateInfo, bool autoStartUpdate = false)
        {
            InitializeComponent();
            _updateService = updateService;
            _updateInfo = updateInfo;
            _autoStartUpdate = autoStartUpdate;

            var current = _updateService.GetCurrentVersion();
            VersionText.Text = $"You have v{current} · New release: v{updateInfo.Version}";
            ReleaseNotesText.Text = updateInfo.ReleaseNotes;

            if (_autoStartUpdate)
            {
                HintText.Text = "Downloading in the background — you can keep using your PC. SharpShot will restart when the update is ready to apply.";
            }

            ApplyThemedButtons();

            MouseDown += (s, e) => { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove(); };

            Loaded += async (_, _) =>
            {
                if (_autoStartUpdate)
                    await StartUpdateAsync();
            };
        }

        private void ApplyThemedButtons()
        {
            var s = SharpShot.App.SettingsService.CurrentSettings;
            var iconColor = string.IsNullOrEmpty(s.IconColor) ? "#FFFF8C00" : s.IconColor;
            var color = (Color)ColorConverter.ConvertFromString(iconColor);

            UpdateCloseButton.Style = ThemeButtonStyleHelper.CreateCloseButtonStyle(color, s.HoverOpacity, s.DropShadowOpacity);
            if (UpdateCloseButton.Content is System.Windows.Controls.TextBlock closeTb)
                closeTb.Foreground = new SolidColorBrush(color);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isUpdating)
                Close();
        }

        private async Task StartUpdateAsync()
        {
            if (_isUpdating) return;

            _isUpdating = true;
            UpdateCloseButton.IsEnabled = false;
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
                        UpdateCloseButton.IsEnabled = true;
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
                    UpdateCloseButton.IsEnabled = true;
                    _isUpdating = false;
                });
            }
        }
    }
}
