using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using SharpShot.Services;

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

            // Populate UI with update info
            VersionText.Text = $"Version {updateInfo.Version} - {updateInfo.ReleaseName}";
            ReleaseNotesText.Text = updateInfo.ReleaseNotes;

            // Make window draggable
            MouseDown += (s, e) => { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove(); };
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

