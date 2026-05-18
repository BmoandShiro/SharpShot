using System.Linq;
using System.Windows;

namespace SharpShot.UI
{
    /// <summary>Theme-matched replacement for <see cref="MessageBox"/>.</summary>
    public static class ThemedMessageBox
    {
        public static MessageBoxResult Show(
            string messageBoxText,
            string caption,
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None)
            => Show(null, messageBoxText, caption, button, icon);

        public static MessageBoxResult Show(
            Window? owner,
            string messageBoxText,
            string caption,
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None)
        {
            owner = ResolveOwner(owner);

            switch (button)
            {
                case MessageBoxButton.OK:
                    ConfirmDialogWindow.ShowAlert(owner, caption, messageBoxText);
                    return MessageBoxResult.OK;

                case MessageBoxButton.OKCancel:
                    return ConfirmDialogWindow.ShowConfirm(owner, caption, messageBoxText, "OK", "Cancel")
                        ? MessageBoxResult.OK
                        : MessageBoxResult.Cancel;

                case MessageBoxButton.YesNo:
                case MessageBoxButton.YesNoCancel:
                    return ConfirmDialogWindow.ShowConfirm(owner, caption, messageBoxText, "Yes", "No")
                        ? MessageBoxResult.Yes
                        : MessageBoxResult.No;

                default:
                    ConfirmDialogWindow.ShowAlert(owner, caption, messageBoxText);
                    return MessageBoxResult.OK;
            }
        }

        public static bool ShowConfirm(
            Window? owner,
            string messageBoxText,
            string caption,
            string confirmText = "Yes",
            string cancelText = "No")
            => ConfirmDialogWindow.ShowConfirm(ResolveOwner(owner), caption, messageBoxText, confirmText, cancelText);

        private static Window? ResolveOwner(Window? owner)
        {
            if (owner != null)
                return owner;

            if (Application.Current?.MainWindow is { IsVisible: true } mainWindow)
                return mainWindow;

            return Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive);
        }
    }
}
