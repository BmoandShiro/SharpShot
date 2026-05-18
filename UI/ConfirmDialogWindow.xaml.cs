using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SharpShot.Utils;

namespace SharpShot.UI
{
    public partial class ConfirmDialogWindow : Window
    {
        private ConfirmDialogWindow(string title, string message, string confirmText, string cancelText, bool showCancel)
        {
            InitializeComponent();

            Title = title;
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;
            ConfirmButtonText.Text = confirmText;
            CancelButtonText.Text = cancelText;
            CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;

            ApplyThemedButtons();
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
        }

        public static bool ShowConfirm(Window? owner, string title, string message,
            string confirmText = "Yes", string cancelText = "No")
        {
            var dialog = new ConfirmDialogWindow(title, message, confirmText, cancelText, showCancel: true);
            if (owner != null)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            return dialog.ShowDialog() == true;
        }

        public static void ShowAlert(Window? owner, string title, string message, string okText = "OK")
        {
            var dialog = new ConfirmDialogWindow(title, message, okText, cancelText: "", showCancel: false);
            if (owner != null)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.ShowDialog();
        }

        private void ApplyThemedButtons()
        {
            var s = App.SettingsService.CurrentSettings;
            var iconColor = string.IsNullOrEmpty(s.IconColor) ? "#FFFF8C00" : s.IconColor;
            var color = (Color)ColorConverter.ConvertFromString(iconColor);
            var brush = new SolidColorBrush(color);

            ConfirmButton.Style = ThemeButtonStyleHelper.CreateModernButtonStyle(
                color, s.HoverOpacity, s.DropShadowOpacity, 100, 36);
            if (ConfirmButton.Content is System.Windows.Controls.TextBlock confirmTb)
                confirmTb.Foreground = brush;

            CancelButton.Style = ThemeButtonStyleHelper.CreateModernButtonStyle(
                color, s.HoverOpacity, s.DropShadowOpacity, 100, 36);
            if (CancelButton.Content is System.Windows.Controls.TextBlock cancelTb)
                cancelTb.Foreground = brush;

            CloseButton.Style = ThemeButtonStyleHelper.CreateCloseButtonStyle(
                color, s.HoverOpacity, s.DropShadowOpacity);
            if (CloseButton.Content is System.Windows.Controls.TextBlock closeTb)
                closeTb.Foreground = brush;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
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
