using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SharpShot.Utils
{
    /// <summary>Builds the same outlined button styles as Settings (accent border, hover tint, glow, pressed).</summary>
    public static class ThemeButtonStyleHelper
    {
        public static Style CreateCloseButtonStyle(Color themeColor, double hoverOpacity, double dropShadowOpacity)
        {
            var style = new Style(typeof(Button));

            style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(themeColor)));
            style.Setters.Add(new Setter(Button.WidthProperty, 30.0));
            style.Setters.Add(new Setter(Button.HeightProperty, 30.0));
            style.Setters.Add(new Setter(Button.FontSizeProperty, 16.0));
            style.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(contentPresenter);
            template.VisualTree = border;

            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            var hoverAlpha = (byte)(hoverOpacity * 255);
            var hoverColor = Color.FromArgb(hoverAlpha, themeColor.R, themeColor.G, themeColor.B);
            hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(hoverColor)));

            var dropShadow = new DropShadowEffect
            {
                Color = themeColor,
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = dropShadowOpacity
            };
            hoverTrigger.Setters.Add(new Setter(Button.EffectProperty, dropShadow));

            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            var pressedColor = Color.FromArgb(48, themeColor.R, themeColor.G, themeColor.B);
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(pressedColor)));

            template.Triggers.Add(hoverTrigger);
            template.Triggers.Add(pressedTrigger);

            style.Setters.Add(new Setter(Button.TemplateProperty, template));

            return style;
        }

        public static Style CreateModernButtonStyle(Color themeColor, double hoverOpacity, double dropShadowOpacity,
            double width, double height, bool allowDynamicWidth = false)
        {
            var style = new Style(typeof(Button));

            style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Button.BorderBrushProperty, new SolidColorBrush(themeColor)));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1.5)));
            style.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(16, 10, 16, 10)));
            style.Setters.Add(new Setter(Button.MarginProperty, new Thickness(4)));
            style.Setters.Add(new Setter(Button.FontSizeProperty, 14.0));
            style.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));

            if (allowDynamicWidth)
            {
                style.Setters.Add(new Setter(Button.MinWidthProperty, width * 0.8));
                style.Setters.Add(new Setter(Button.MaxWidthProperty, width * 1.3));
            }
            else
            {
                style.Setters.Add(new Setter(Button.WidthProperty, width));
            }

            style.Setters.Add(new Setter(Button.HeightProperty, height));

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(contentPresenter);
            template.VisualTree = border;

            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            var hoverAlpha = (byte)(hoverOpacity * 255);
            var hoverColor = Color.FromArgb(hoverAlpha, themeColor.R, themeColor.G, themeColor.B);
            hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(hoverColor)));

            var dropShadow = new DropShadowEffect
            {
                Color = themeColor,
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = dropShadowOpacity
            };
            hoverTrigger.Setters.Add(new Setter(Button.EffectProperty, dropShadow));

            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            var pressedColor = Color.FromArgb(48, themeColor.R, themeColor.G, themeColor.B);
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(pressedColor)));
            pressedTrigger.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(2)));

            template.Triggers.Add(hoverTrigger);
            template.Triggers.Add(pressedTrigger);

            style.Setters.Add(new Setter(Button.TemplateProperty, template));

            return style;
        }
    }
}
