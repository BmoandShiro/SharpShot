using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SharpShot.Utils;

namespace SharpShot.UI
{
    public partial class AppLinkDialog : Window
    {
        public string? SelectedExecutablePath { get; private set; }
        public string? SelectedDisplayName { get; private set; }

        private readonly bool _obsMode;

        private AppLinkDialog(bool obsMode)
        {
            InitializeComponent();
            _obsMode = obsMode;

            TitleTextBlock.Text = obsMode ? "Link OBS Studio" : "Link Custom Application";
            MessageTextBlock.Text = obsMode
                ? "Select OBS Studio from the list, or browse to obs64.exe."
                : "Select an installed application, or browse for any .exe.";

            MouseLeftButtonDown += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    DragMove();
            };
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    Close();
                    e.Handled = true;
                }
            };

            LoadApps();
        }

        /// <summary>
        /// Shows the custom-app linker (installed apps list + Browse).
        /// Returns true if the user linked an executable.
        /// </summary>
        public static bool TryLinkCustomApp(Window? owner, out string executablePath, out string displayName)
        {
            executablePath = string.Empty;
            displayName = string.Empty;
            var dlg = new AppLinkDialog(obsMode: false);
            if (owner != null)
                dlg.Owner = owner;
            var result = dlg.ShowDialog() == true
                && !string.IsNullOrWhiteSpace(dlg.SelectedExecutablePath);
            if (!result)
                return false;

            executablePath = dlg.SelectedExecutablePath!;
            displayName = string.IsNullOrWhiteSpace(dlg.SelectedDisplayName)
                ? Path.GetFileNameWithoutExtension(executablePath)
                : dlg.SelectedDisplayName!;
            return true;
        }

        /// <summary>
        /// OBS link: auto-detect first; if that fails, show list filtered toward OBS + Browse.
        /// </summary>
        public static bool TryLinkObs(Window? owner, out string executablePath)
        {
            executablePath = string.Empty;

            var detected = OBSDetection.FindOBSPath();
            if (!string.IsNullOrEmpty(detected) && OBSDetection.IsValidLinkedObsPath(detected))
            {
                executablePath = detected;
                return true;
            }

            var dlg = new AppLinkDialog(obsMode: true);
            if (owner != null)
                dlg.Owner = owner;
            var result = dlg.ShowDialog() == true
                && OBSDetection.IsValidLinkedObsPath(dlg.SelectedExecutablePath);
            if (!result)
                return false;

            executablePath = dlg.SelectedExecutablePath!;
            return true;
        }

        /// <summary>Browse-only fallback for Settings "Browse…" on OBS.</summary>
        public static bool TryBrowseForObs(Window? owner, out string executablePath)
        {
            executablePath = string.Empty;
            var dialog = new OpenFileDialog
            {
                Title = "Select OBS Studio executable",
                Filter = "OBS Studio|obs64.exe;obs32.exe|Executables (*.exe)|*.exe",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(owner) != true)
                return false;

            if (!OBSDetection.IsValidLinkedObsPath(dialog.FileName))
            {
                ThemedMessageBox.Show(owner, "Please select obs64.exe or obs32.exe.", "Link OBS",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            executablePath = dialog.FileName;
            return true;
        }

        private void LoadApps()
        {
            try
            {
                var apps = InstalledAppEnumerator.GetInstalledApps();
                if (_obsMode)
                {
                    apps = apps
                        .Where(a =>
                            a.DisplayName.Contains("OBS", StringComparison.OrdinalIgnoreCase)
                            || a.ExecutablePath.Contains("obs-studio", StringComparison.OrdinalIgnoreCase)
                            || Path.GetFileName(a.ExecutablePath).StartsWith("obs", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                AppsListBox.ItemsSource = apps;
                if (apps.Count == 0)
                {
                    MessageTextBlock.Text += " No matching Start Menu apps found — use Browse…";
                }
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not enumerate installed apps: {ex.Message}. Use Browse…";
            }
        }

        private void AppsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (AppsListBox.SelectedItem is InstalledAppInfo app)
            {
                SelectedExecutablePath = app.ExecutablePath;
                SelectedDisplayName = app.DisplayName;
                SelectedPathTextBlock.Text = app.ExecutablePath;
                LinkButton.IsEnabled = true;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = _obsMode ? "Select OBS Studio executable" : "Select application executable",
                Filter = _obsMode
                    ? "OBS Studio|obs64.exe;obs32.exe|Executables (*.exe)|*.exe"
                    : "Executables (*.exe)|*.exe",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != true)
                return;

            if (_obsMode && !OBSDetection.IsValidLinkedObsPath(dialog.FileName))
            {
                ThemedMessageBox.Show(this, "Please select obs64.exe or obs32.exe.", "Link OBS",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedExecutablePath = dialog.FileName;
            SelectedDisplayName = Path.GetFileNameWithoutExtension(dialog.FileName);
            SelectedPathTextBlock.Text = dialog.FileName;
            LinkButton.IsEnabled = true;
            AppsListBox.SelectedItem = null;
        }

        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedExecutablePath) || !File.Exists(SelectedExecutablePath))
            {
                ThemedMessageBox.Show(this, "Please select a valid executable.", "Link Application",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_obsMode && !OBSDetection.IsValidLinkedObsPath(SelectedExecutablePath))
            {
                ThemedMessageBox.Show(this, "Please select obs64.exe or obs32.exe.", "Link OBS",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
