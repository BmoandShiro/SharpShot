using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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

        /// <summary>
        /// Small taskbar / alt-tab icon (16 or 32). Caller owns and must Dispose.
        /// </summary>
        public static Icon CreateTaskbarIconSmall(string? colorHex)
        {
            var color = ParseColor(colorHex);
            using var bmp = TintEmbeddedPng(Resource16, color) ?? TintEmbeddedPng(Resource32, color);
            if (bmp == null)
                return SystemIcons.Application;
            return CreateOwnedIcon(bmp);
        }

        /// <summary>
        /// Large taskbar / alt-tab icon (128 preferred). Caller owns and must Dispose.
        /// </summary>
        public static Icon CreateTaskbarIconBig(string? colorHex)
        {
            var color = ParseColor(colorHex);
            using var bmp = TintEmbeddedPng(Resource128, color)
                ?? TintEmbeddedPng(Resource32, color)
                ?? TintEmbeddedPng(Resource16, color);
            if (bmp == null)
                return SystemIcons.Application;
            return CreateOwnedIcon(bmp);
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

        /// <summary>
        /// Writes a multi-size PNG-in-ICO file for Start Menu / pinned taskbar shortcuts.
        /// </summary>
        public static void SaveThemedIcoFile(string path, string? colorHex)
        {
            var color = ParseColor(colorHex);
            var pngBlobs = new List<byte[]>();

            void AddPng(string resourceName)
            {
                using var bmp = TintEmbeddedPng(resourceName, color);
                if (bmp == null)
                    return;
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                pngBlobs.Add(ms.ToArray());
            }

            AddPng(Resource16);
            AddPng(Resource32);
            AddPng(Resource128);

            if (pngBlobs.Count == 0)
                throw new InvalidOperationException("No themed icon resources available.");

            WritePngIco(path, pngBlobs);
        }

        private static void WritePngIco(string path, IReadOnlyList<byte[]> pngImages)
        {
            // ICONDIR (6) + ICONDIRENTRY (16 * n) + PNG payloads
            const int dirSize = 6;
            const int entrySize = 16;
            int offset = dirSize + (entrySize * pngImages.Count);

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var bw = new BinaryWriter(fs);

            bw.Write((ushort)0); // reserved
            bw.Write((ushort)1); // type = icon
            bw.Write((ushort)pngImages.Count);

            var payloads = new List<(byte width, byte height, byte[] data)>();
            foreach (var png in pngImages)
            {
                byte w = 0, h = 0;
                // IHDR width/height at bytes 16..23 of a PNG (big-endian)
                if (png.Length >= 24)
                {
                    int fullW = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
                    int fullH = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
                    w = (byte)(fullW >= 256 ? 0 : fullW);
                    h = (byte)(fullH >= 256 ? 0 : fullH);
                }

                payloads.Add((w, h, png));
            }

            foreach (var (width, height, data) in payloads)
            {
                bw.Write(width);
                bw.Write(height);
                bw.Write((byte)0); // color count
                bw.Write((byte)0); // reserved
                bw.Write((ushort)1); // planes
                bw.Write((ushort)32); // bit count
                bw.Write(data.Length);
                bw.Write(offset);
                offset += data.Length;
            }

            foreach (var (_, _, data) in payloads)
                bw.Write(data);
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
