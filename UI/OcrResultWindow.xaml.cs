using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SharpShot.Services;

namespace SharpShot.UI
{
    public partial class OcrResultWindow : Window
    {
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
            try
            {
                Clipboard.SetText(ResultTextBox.Text ?? string.Empty);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Failed to copy text: {ex.Message}", "OCR Result", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

    }
}
