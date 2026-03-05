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
    /// Result of one recognized word from OCR, with bounding box in image pixel coordinates.
    /// </summary>
    public sealed class OcrWordResult
    {
        public string Text { get; init; } = "";
        public double X { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
    }

    /// <summary>
    /// Uses Tesseract OCR to extract text from images. Requires tessdata (e.g. eng.traineddata) in the application directory or a tessdata subfolder.
    /// </summary>
    public static class OcrService
    {
        private static string GetTessDataPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // Prefer tessdata subfolder; then base dir if eng.traineddata is there; then parent tessdata
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

        /// <summary>
        /// Checks if OCR is available (Tesseract engine and tessdata can be loaded).
        /// </summary>
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

        /// <summary>
        /// Runs OCR on the given bitmap and returns word-level results with bounding rectangles in image coordinates.
        /// Large images are scaled down for speed, then coordinates are scaled back to the original size.
        /// </summary>
        public static async Task<IReadOnlyList<OcrWordResult>> RecognizeWordsAsync(Bitmap bitmap)
        {
            if (bitmap == null)
                return Array.Empty<OcrWordResult>();

            return await Task.Run(() =>
            {
                var list = new List<OcrWordResult>();
                try
                {
                    const int maxSide = 1600; // OCR on a smaller image is much faster
                    var w = bitmap.Width;
                    var h = bitmap.Height;
                    Bitmap? toProcess = null;
                    double scaleX = 1.0;
                    double scaleY = 1.0;
                    if (w > maxSide || h > maxSide)
                    {
                        if (w >= h)
                        {
                            scaleX = scaleY = (double)maxSide / w;
                            w = maxSide;
                            h = (int)(bitmap.Height * scaleY);
                        }
                        else
                        {
                            scaleX = scaleY = (double)maxSide / h;
                            h = maxSide;
                            w = (int)(bitmap.Width * scaleX);
                        }
                        toProcess = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                        using (var g = Graphics.FromImage(toProcess))
                        {
                            g.DrawImage(bitmap, 0, 0, w, h);
                        }
                        scaleX = (double)bitmap.Width / w;
                        scaleY = (double)bitmap.Height / h;
                    }
                    else
                    {
                        toProcess = bitmap;
                    }

                    var tessDataPath = GetTessDataPath();
                    using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                    using var page = engine.Process(toProcess);
                    using var iter = page.GetIterator();
                    iter.Begin();
                    do
                    {
                        if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out var rect))
                        {
                            var text = iter.GetText(PageIteratorLevel.Word)?.Trim();
                            if (!string.IsNullOrEmpty(text))
                            {
                                var width = rect.X2 - rect.X1;
                                var height = rect.Y2 - rect.Y1;
                                list.Add(new OcrWordResult
                                {
                                    Text = text,
                                    X = rect.X1 * scaleX,
                                    Y = rect.Y1 * scaleY,
                                    Width = width * scaleX,
                                    Height = height * scaleY
                                });
                            }
                        }
                    } while (iter.Next(PageIteratorLevel.Word));

                    if (toProcess != null && toProcess != bitmap)
                        toProcess.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OcrService error: {ex.Message}");
                }
                return list;
            }).ConfigureAwait(false);
        }
    }
}
