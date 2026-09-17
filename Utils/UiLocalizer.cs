using System.Windows;
using System.Windows.Controls;
using SharpShot.Services;

namespace SharpShot.Utils
{
    /// <summary>
    /// Applies the current app language to labeled UI text and tooltips.
    /// The English source is stored so later language switches do not translate a translation.
    /// Dynamic values (paths, device names, numbers) are left alone unless set with <see cref="SetText"/>.
    /// </summary>
    internal static class UiLocalizer
    {
        private static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
            "Source",
            typeof(string),
            typeof(UiLocalizer),
            new PropertyMetadata(null));

        private static readonly DependencyProperty SkipProperty = DependencyProperty.RegisterAttached(
            "Skip",
            typeof(bool),
            typeof(UiLocalizer),
            new PropertyMetadata(false));

        public static void Apply(DependencyObject? root)
        {
            if (root == null)
                return;
            ApplyElement(root);
            foreach (var child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject next)
                    Apply(next);
            }
        }

        public static void ApplyOpenWindows()
        {
            var app = Application.Current;
            if (app == null)
                return;
            foreach (Window window in app.Windows)
                Apply(window);
        }

        public static string ItemValue(ComboBoxItem item)
        {
            var source = item.GetValue(SourceProperty) as string;
            return string.IsNullOrEmpty(source) ? item.Content?.ToString() ?? string.Empty : source;
        }

        public static void SetText(DependencyObject target, string english)
        {
            target.SetValue(SkipProperty, false);
            target.SetValue(SourceProperty, english);
            Write(target, LocalizationService.Translate(english));
        }

        public static void SetRaw(DependencyObject target, string value)
        {
            target.SetValue(SkipProperty, true);
            target.ClearValue(SourceProperty);
            Write(target, value);
        }

        private static void ApplyElement(DependencyObject node)
        {
            if (node.GetValue(SkipProperty) is true)
                return;
            if (node is TextBox)
                return;

            if (node is TextBlock textBlock)
                ApplyString(textBlock, textBlock.Text, value => textBlock.Text = value, SourceProperty);
            else if (node is ContentControl control && control.Content is string content)
                ApplyString(control, content, value => control.Content = value, SourceProperty);

            if (node is HeaderedContentControl headered && headered.Header is string header)
                ApplyString(headered, header, value => headered.Header = value, HeaderSourceProperty);

            if (node is FrameworkElement element && element.ToolTip is string tip)
                ApplyString(element, tip, value => element.ToolTip = value, TooltipSourceProperty);

            if (node is Window window && !string.IsNullOrEmpty(window.Title))
                ApplyString(window, window.Title, value => window.Title = value, TitleSourceProperty);
        }

        private static readonly DependencyProperty TooltipSourceProperty = DependencyProperty.RegisterAttached(
            "TooltipSource",
            typeof(string),
            typeof(UiLocalizer),
            new PropertyMetadata(null));

        private static readonly DependencyProperty HeaderSourceProperty = DependencyProperty.RegisterAttached(
            "HeaderSource",
            typeof(string),
            typeof(UiLocalizer),
            new PropertyMetadata(null));

        private static readonly DependencyProperty TitleSourceProperty = DependencyProperty.RegisterAttached(
            "TitleSource",
            typeof(string),
            typeof(UiLocalizer),
            new PropertyMetadata(null));

        private static void ApplyString(DependencyObject element, string current, System.Action<string> write, DependencyProperty sourceProperty)
        {
            var source = element.GetValue(sourceProperty) as string;
            if (string.IsNullOrEmpty(source))
            {
                if (!LocalizationService.HasPhrase(current))
                    return;
                source = current;
                element.SetValue(sourceProperty, source);
            }
            write(LocalizationService.Translate(source));
        }

        private static void Write(DependencyObject target, string value)
        {
            switch (target)
            {
                case TextBlock text:
                    text.Text = value;
                    break;
                case ContentControl control:
                    control.Content = value;
                    break;
            }
        }
    }
}
