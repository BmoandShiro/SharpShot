using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SharpShot.Services;

namespace SharpShot.UI
{
    public partial class OcrResultWindow : Window
    {
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        public OcrResultWindow(string extractedText)
        {
            InitializeComponent();
            ResultTextBox.Text = extractedText ?? string.Empty;
            ApplyThemeAwareButtonStyles();
        }

        private void ApplyThemeAwareButtonStyles()
        {
            try
            {
                var iconColor = App.SettingsService?.CurrentSettings?.IconColor;
                if (string.IsNullOrWhiteSpace(iconColor))
                {
                    return;
                }

                if (ColorConverter.ConvertFromString(iconColor) is not Color themeColor)
                {
                    return;
                }

                CopyTextButton.Style = CreateThemeAwareButtonStyle(themeColor);
                CloseButton.Style = CreateThemeAwareButtonStyle(themeColor);
                HeaderCloseButton.Style = CreateThemeAwareButtonStyle(themeColor);
            }
            catch
            {
                // Keep default styles if theme style creation fails.
            }
        }

        private static Style CreateThemeAwareButtonStyle(Color themeColor)
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(themeColor)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
            style.Setters.Add(new Setter(Control.TemplateProperty, BuildButtonTemplate()));

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty,
                new SolidColorBrush(Color.FromArgb(38, themeColor.R, themeColor.G, themeColor.B))));
            hoverTrigger.Setters.Add(new Setter(UIElement.EffectProperty, new DropShadowEffect
            {
                Color = themeColor,
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.25
            }));

            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Control.BackgroundProperty,
                new SolidColorBrush(Color.FromArgb(77, themeColor.R, themeColor.G, themeColor.B))));
            pressedTrigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));

            style.Triggers.Add(hoverTrigger);
            style.Triggers.Add(pressedTrigger);
            return style;
        }

        private static ControlTemplate BuildButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;
            return template;
        }

        private void CopyTextButton_Click(object sender, RoutedEventArgs e)
        {
            // Win32 clipboard — avoids WPF CLIPBRD_E_CANT_OPEN noise and Focus/SelectAll stutter.
            TryCopyTextToClipboard(ResultTextBox.Text ?? string.Empty, out _);
        }

        /// <summary>
        /// Fast clipboard write via Win32. No Focus/SelectAll, no UI-thread Sleep.
        /// </summary>
        internal static bool TryCopyTextToClipboard(string text, out string error)
        {
            error = string.Empty;
            text ??= string.Empty;

            // A couple of immediate retries — other apps often release the clipboard within microseconds.
            // Avoid Sleep/busy-wait on the UI thread (that was the Copy button stutter).
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (TrySetClipboardTextWin32(text))
                    return true;
            }

            error = "OpenClipboard Failed";
            return false;
        }

        private static bool TrySetClipboardTextWin32(string text)
        {
            IntPtr hGlobal = IntPtr.Zero;
            try
            {
                if (!OpenClipboard(IntPtr.Zero))
                    return false;

                try
                {
                    EmptyClipboard();

                    var bytes = (text.Length + 1) * 2;
                    hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(uint)bytes);
                    if (hGlobal == IntPtr.Zero)
                        return false;

                    IntPtr locked = GlobalLock(hGlobal);
                    if (locked == IntPtr.Zero)
                    {
                        GlobalFree(hGlobal);
                        hGlobal = IntPtr.Zero;
                        return false;
                    }

                    try
                    {
                        Marshal.Copy(text.ToCharArray(), 0, locked, text.Length);
                        Marshal.WriteInt16(locked, text.Length * 2, 0);
                    }
                    finally
                    {
                        GlobalUnlock(hGlobal);
                    }

                    if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                    {
                        GlobalFree(hGlobal);
                        hGlobal = IntPtr.Zero;
                        return false;
                    }

                    // Ownership transferred to the clipboard
                    hGlobal = IntPtr.Zero;
                    return true;
                }
                finally
                {
                    CloseClipboard();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Win32 clipboard failed: {ex.Message}");
                if (hGlobal != IntPtr.Zero)
                {
                    try { GlobalFree(hGlobal); } catch { /* ignore */ }
                }
                return false;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

    }
}
