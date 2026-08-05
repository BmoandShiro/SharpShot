using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace SharpShot.Utils
{
    /// <summary>
    /// Builds tray/window icons tinted to the user's theme (IconColor).
    /// Base assets are orange-on-black; non-black pixels are recolored while alpha is preserved.
    /// </summary>
    public static class ThemedIconHelper
    {
        private const string Resource16 = "SharpShot.Resources.tray_icon_16.png";
        private const string Resource32 = "SharpShot.Resources.tray_icon_32.png";
        private const string Resource128 = "SharpShot.Resources.app_icon_128.png";
        private const string DefaultHex = "#FFFF8C00";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon CreateTrayIcon(string? colorHex)
        {
            var color = ParseColor(colorHex);
            using var bmp16 = TintEmbeddedPng(Resource16, color);
            using var bmp32 = TintEmbeddedPng(Resource32, color);
            // Prefer 16x16 for the notification area; fall back to 32 if needed.
            var source = bmp16 ?? bmp32;
            if (source == null)
                return SystemIcons.Application;

            return CreateOwnedIcon(source);
        }

        public static BitmapSource CreateWindowIcon(string? colorHex)
        {
            var color = ParseColor(colorHex);
            using var bmp = TintEmbeddedPng(Resource128, color)
                ?? TintEmbeddedPng(Resource32, color)
                ?? TintEmbeddedPng(Resource16, color);

            if (bmp == null)
                return new BitmapImage();

            using var icon = CreateOwnedIcon(bmp);
            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }

        private static Icon CreateOwnedIcon(Bitmap bmp)
        {
            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var temp = Icon.FromHandle(hIcon);
                return (Icon)temp.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        private static Bitmap? TintEmbeddedPng(string resourceName, DrawingColor theme)
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var original = new Bitmap(stream);
            return TintBitmap(original, theme);
        }

        internal static Bitmap TintBitmap(Bitmap original, DrawingColor theme)
        {
            var result = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);

            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    var px = original.GetPixel(x, y);
                    if (px.A == 0)
                    {
                        result.SetPixel(x, y, DrawingColor.Transparent);
                        continue;
                    }

                    // Near-black fill stays black (icon plate).
                    if (px.R < 28 && px.G < 28 && px.B < 28)
                    {
                        result.SetPixel(x, y, DrawingColor.FromArgb(px.A, 0, 0, 0));
                        continue;
                    }

                    // Scale theme RGB by original luminance so anti-aliased edges stay smooth.
                    float intensity = Math.Max(px.R, Math.Max(px.G, px.B)) / 255f;
                    byte r = (byte)Math.Clamp((int)Math.Round(theme.R * intensity), 0, 255);
                    byte g = (byte)Math.Clamp((int)Math.Round(theme.G * intensity), 0, 255);
                    byte b = (byte)Math.Clamp((int)Math.Round(theme.B * intensity), 0, 255);
                    result.SetPixel(x, y, DrawingColor.FromArgb(px.A, r, g, b));
                }
            }

            return result;
        }

        private static DrawingColor ParseColor(string? colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
                colorHex = DefaultHex;

            try
            {
                var media = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                return DrawingColor.FromArgb(media.A, media.R, media.G, media.B);
            }
            catch
            {
                return DrawingColor.FromArgb(255, 255, 140, 0);
            }
        }
    }
}
