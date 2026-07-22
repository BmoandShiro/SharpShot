using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tesseract;

namespace SharpShot.Services
{
    /// <summary>
    /// Result of one recognized word/line from OCR, with bounding box in image pixel coordinates.
    /// </summary>
    public sealed class OcrWordResult
    {
        public string Text { get; init; } = "";
        public double X { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        /// <summary>Tesseract mean confidence 0–100 for this unit (0 if unknown).</summary>
        public float Confidence { get; init; }
    }

    /// <summary>
    /// Uses Tesseract OCR to extract text from images. Requires tessdata (e.g. eng.traineddata) in the application directory or a tessdata subfolder.
    /// </summary>
    public static class OcrService
    {
        public const float DefaultMinConfidence = 55f;

        private static string GetTessDataPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var tessDataSub = Path.Combine(baseDir, "tessdata");
            if (Directory.Exists(tessDataSub))
                return Path.GetFullPath(tessDataSub);
            if (File.Exists(Path.Combine(tessDataSub, "eng.traineddata")))
                return Path.GetFullPath(tessDataSub);
            if (File.Exists(Path.Combine(baseDir, "eng.traineddata")))
                return Path.GetFullPath(baseDir);
            var parentTess = Path.Combine(baseDir, "..", "tessdata");
            var parentTessFull = Path.GetFullPath(parentTess);
            if (Directory.Exists(parentTessFull) || File.Exists(Path.Combine(parentTessFull, "eng.traineddata")))
                return parentTessFull;
            return Path.GetFullPath(tessDataSub);
        }

        public static bool IsAvailable()
        {
            try
            {
                var tessDataPath = GetTessDataPath();
                using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OcrService.IsAvailable: {ex.Message}");
                return false;
            }
        }

        public static Task<IReadOnlyList<OcrWordResult>> RecognizeWordsAsync(Bitmap bitmap)
            => RecognizeAsync(bitmap, PageIteratorLevel.Word, maxSide: 1600, PageSegMode.Auto, DefaultMinConfidence);

        /// <summary>
        /// Single-pass text-line OCR (SparseText). Prefer <see cref="RecognizeSmartRegionLinesAsync"/> for region highlights.
        /// </summary>
        public static Task<IReadOnlyList<OcrWordResult>> RecognizeTextLinesAsync(Bitmap bitmap)
            => RecognizeAsync(bitmap, PageIteratorLevel.TextLine, maxSide: 2200, PageSegMode.SparseText, DefaultMinConfidence);

        /// <summary>
        /// High-coverage OCR for smart regions: overlapping tiles, always SparseText + Auto,
        /// confidence filtering, coordinates in full-image space.
        /// </summary>
        public static async Task<IReadOnlyList<OcrWordResult>> RecognizeSmartRegionLinesAsync(Bitmap bitmap)
        {
            if (bitmap == null)
                return Array.Empty<OcrWordResult>();

            // Prepare once for the whole image so invert decision is consistent across tiles.
            Bitmap prepared;
            try
            {
                prepared = PrepareForOcr(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OcrService prepare failed: {ex.Message}");
                return Array.Empty<OcrWordResult>();
            }

            const float denseMinConfidence = 45f;

            try
            {
                var tiles = BuildOverlapTiles(prepared.Width, prepared.Height);
                var all = new List<OcrWordResult>();

                foreach (var tile in tiles)
                {
                    using var crop = prepared.Clone(tile, PixelFormat.Format24bppRgb);
                    // Keep tiles sharp — little/no downscale for typical tile sizes.
                    var sparse = await RecognizePreparedAsync(crop, PageIteratorLevel.TextLine, maxSide: 1800,
                        PageSegMode.SparseText, denseMinConfidence).ConfigureAwait(false);
                    OffsetResults(sparse, tile.X, tile.Y, all);

                    // Always run Auto as well — SparseText alone misses dense prose (Opera paragraphs).
                    var auto = await RecognizePreparedAsync(crop, PageIteratorLevel.TextLine, maxSide: 1800,
                        PageSegMode.Auto, denseMinConfidence).ConfigureAwait(false);
                    OffsetResults(auto, tile.X, tile.Y, all);
                }

                return DedupLines(all);
            }
            finally
            {
                prepared.Dispose();
            }
        }

        private static List<Rectangle> BuildOverlapTiles(int width, int height)
        {
            var tiles = new List<Rectangle>();
            if (width <= 0 || height <= 0) return tiles;

            // Smaller overlapping tiles → higher effective DPI and fewer missed mid-page lines.
            int tileH = height <= 850 ? height : 800;
            int tileW = width <= 1100 ? width : 1000;
            int overlapY = Math.Max(60, (int)(tileH * 0.18));
            int overlapX = Math.Max(60, (int)(tileW * 0.15));
            int stepY = Math.Max(1, tileH - overlapY);
            int stepX = Math.Max(1, tileW - overlapX);

            for (int y = 0; y < height; y += stepY)
            {
                int h = Math.Min(tileH, height - y);
                if (h < 40) break;
                for (int x = 0; x < width; x += stepX)
                {
                    int w = Math.Min(tileW, width - x);
                    if (w < 40) break;
                    tiles.Add(new Rectangle(x, y, w, h));
                    if (x + w >= width) break;
                }
                if (y + h >= height) break;
            }

            if (tiles.Count == 0)
                tiles.Add(new Rectangle(0, 0, width, height));

            return tiles;
        }

        private static void OffsetResults(IReadOnlyList<OcrWordResult> src, int ox, int oy, List<OcrWordResult> dest)
        {
            foreach (var r in src)
            {
                dest.Add(new OcrWordResult
                {
                    Text = r.Text,
                    X = r.X + ox,
                    Y = r.Y + oy,
                    Width = r.Width,
                    Height = r.Height,
                    Confidence = r.Confidence
                });
            }
        }

        private static IReadOnlyList<OcrWordResult> DedupLines(List<OcrWordResult> lines)
        {
            if (lines.Count <= 1) return lines;

            var ordered = lines
                .OrderByDescending(l => l.Confidence)
                .ThenByDescending(l => l.Text.Length)
                .ToList();

            var kept = new List<OcrWordResult>();
            foreach (var line in ordered)
            {
                bool redundant = false;
                var a = new RectangleF((float)line.X, (float)line.Y, (float)line.Width, (float)line.Height);
                foreach (var existing in kept)
                {
                    var b = new RectangleF((float)existing.X, (float)existing.Y, (float)existing.Width, (float)existing.Height);
                    var inter = RectangleF.Intersect(a, b);
                    if (inter.IsEmpty) continue;
                    float ratio = (inter.Width * inter.Height) / Math.Max(1f, Math.Min(a.Width * a.Height, b.Width * b.Height));
                    if (ratio > 0.7f)
                    {
                        redundant = true;
                        break;
                    }
                }
                if (!redundant)
                    kept.Add(line);
            }

            return kept.OrderBy(l => l.Y).ThenBy(l => l.X).ToList();
        }

        private static async Task<IReadOnlyList<OcrWordResult>> RecognizeAsync(
            Bitmap bitmap, PageIteratorLevel level, int maxSide, PageSegMode segMode, float minConfidence)
        {
            if (bitmap == null)
                return Array.Empty<OcrWordResult>();

            Bitmap workCopy;
            try
            {
                workCopy = PrepareForOcr(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OcrService prepare failed: {ex.Message}");
                return Array.Empty<OcrWordResult>();
            }

            try
            {
                return await RecognizePreparedAsync(workCopy, level, maxSide, segMode, minConfidence).ConfigureAwait(false);
            }
            finally
            {
                workCopy.Dispose();
            }
        }

        /// <summary>
        /// OCR a bitmap that is already prepared (grayscale/inverted). Does not dispose <paramref name="prepared"/>.
        /// </summary>
        private static Task<IReadOnlyList<OcrWordResult>> RecognizePreparedAsync(
            Bitmap prepared, PageIteratorLevel level, int maxSide, PageSegMode segMode, float minConfidence)
        {
            // Clone for the worker thread — GDI+ bitmaps are not cross-thread safe.
            Bitmap workCopy;
            try
            {
                workCopy = new Bitmap(prepared.Width, prepared.Height, PixelFormat.Format24bppRgb);
                using var g = Graphics.FromImage(workCopy);
                g.DrawImage(prepared, 0, 0, prepared.Width, prepared.Height);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OcrService clone failed: {ex.Message}");
                return Task.FromResult<IReadOnlyList<OcrWordResult>>(Array.Empty<OcrWordResult>());
            }

            return Task.Run(() =>
            {
                var list = new List<OcrWordResult>();
                try
                {
                    var w = workCopy.Width;
                    var h = workCopy.Height;
                    Bitmap? toProcess = workCopy;
                    double scaleX = 1.0;
                    double scaleY = 1.0;
                    bool scaled = false;
                    if (w > maxSide || h > maxSide)
                    {
                        if (w >= h)
                        {
                            scaleX = scaleY = (double)maxSide / w;
                            w = maxSide;
                            h = (int)(workCopy.Height * scaleY);
                        }
                        else
                        {
                            scaleX = scaleY = (double)maxSide / h;
                            h = maxSide;
                            w = (int)(workCopy.Width * scaleX);
                        }
                        toProcess = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                        using (var g = Graphics.FromImage(toProcess))
                        {
                            g.DrawImage(workCopy, 0, 0, w, h);
                        }
                        scaleX = (double)workCopy.Width / w;
                        scaleY = (double)workCopy.Height / h;
                        scaled = true;
                    }

                    var tessDataPath = GetTessDataPath();
                    using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                    engine.SetVariable("user_defined_dpi", "96");
                    using var page = engine.Process(toProcess, segMode);
                    using var iter = page.GetIterator();
                    iter.Begin();
                    do
                    {
                        if (!iter.TryGetBoundingBox(level, out var rect))
                            continue;

                        var text = iter.GetText(level)?.Trim();
                        if (string.IsNullOrEmpty(text))
                            continue;

                        float conf = 0;
                        try { conf = iter.GetConfidence(level); }
                        catch { /* older tessdata / level */ }

                        if (conf > 0 && conf < minConfidence)
                            continue;

                        var width = rect.X2 - rect.X1;
                        var height = rect.Y2 - rect.Y1;
                        list.Add(new OcrWordResult
                        {
                            Text = text,
                            X = rect.X1 * scaleX,
                            Y = rect.Y1 * scaleY,
                            Width = width * scaleX,
                            Height = height * scaleY,
                            Confidence = conf
                        });
                    } while (iter.Next(level));

                    if (scaled && toProcess != null && !ReferenceEquals(toProcess, workCopy))
                        toProcess.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OcrService error: {ex.Message}");
                }
                finally
                {
                    workCopy.Dispose();
                }
                return (IReadOnlyList<OcrWordResult>)list;
            });
        }

        /// <summary>
        /// Grayscale + optional invert for dark UIs. Tesseract expects dark text on light background.
        /// </summary>
        private static Bitmap PrepareForOcr(Bitmap source)
        {
            int w = source.Width;
            int h = source.Height;

            var src24 = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(src24))
                g.DrawImage(source, 0, 0, w, h);

            var srcData = src24.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = Math.Abs(srcData.Stride);
                int bytes = stride * h;
                var buffer = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, buffer, 0, bytes);

                long lumSum = 0;
                int samples = 0;
                int stepY = Math.Max(1, h / 32);
                int stepX = Math.Max(1, w / 32);
                for (int y = 0; y < h; y += stepY)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x += stepX)
                    {
                        int i = row + x * 3;
                        byte b = buffer[i], gch = buffer[i + 1], r = buffer[i + 2];
                        lumSum += (r * 30 + gch * 59 + b * 11) / 100;
                        samples++;
                    }
                }
                bool invert = samples > 0 && (lumSum / samples) < 110;

                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + x * 3;
                        int lum = (buffer[i + 2] * 30 + buffer[i + 1] * 59 + buffer[i] * 11) / 100;
                        lum = (int)Math.Clamp((lum - 16) * 1.3, 0, 255);
                        if (invert) lum = 255 - lum;
                        byte v = (byte)lum;
                        buffer[i] = v;
                        buffer[i + 1] = v;
                        buffer[i + 2] = v;
                    }
                }

                var result = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                var dstData = result.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(buffer, 0, dstData.Scan0, Math.Min(bytes, Math.Abs(dstData.Stride) * h));
                }
                finally
                {
                    result.UnlockBits(dstData);
                }
                return result;
            }
            finally
            {
                src24.UnlockBits(srcData);
                src24.Dispose();
            }
        }
    }
}
