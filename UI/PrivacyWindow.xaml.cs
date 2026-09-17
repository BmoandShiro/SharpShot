using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using SharpShot.Services;
using SharpShot.Utils;

namespace SharpShot.UI
{
    public partial class PrivacyWindow : Window
    {
        public const string LiveUrl = "https://github.com/BmoandShiro/SharpShot/blob/main/PRIVACY.md";

        public PrivacyWindow()
        {
            InitializeComponent();
            var policy = LocalizationService.PrivacyText();
            PolicyText.Text = string.IsNullOrWhiteSpace(policy) ? LoadPolicyText() : policy;
            UiLocalizer.SetRaw(PolicyText, PolicyText.Text);
            UiLocalizer.Apply(this);
        }

        public static void Show(Window? owner)
        {
            var window = new PrivacyWindow();
            if (owner != null)
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            window.ShowDialog();
        }

        private static string LoadPolicyText()
        {
            foreach (var path in new[]
            {
                Path.Combine(AppContext.BaseDirectory, "PRIVACY.md"),
                Path.Combine(Directory.GetCurrentDirectory(), "PRIVACY.md")
            })
            {
                try
                {
                    if (File.Exists(path))
                        return File.ReadAllText(path);
                }
                catch
                {
                    // try the next location
                }
            }

            return "SharpShot does not run accounts, ads, or analytics. Screenshots and settings stay on this PC."
                   + Environment.NewLine + Environment.NewLine
                   + "The full privacy policy is published at:" + Environment.NewLine
                   + LiveUrl;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); }
                catch (InvalidOperationException) { }
            }
        }
    }
}
