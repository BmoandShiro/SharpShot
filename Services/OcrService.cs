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
    /// Uses Tesseract OCR to extract text from images. Requires tessdata (e.g. eng.traineddata)
    /// next to SharpShot.exe (or staged under %LocalAppData%\SharpShot for read-only Store installs).
    /// </summary>
    public static class OcrService
    {
        public const float DefaultMinConfidence = 55f;
        private static readonly object TessDataSync = new();
        private static string? _resolvedTessDataFolder;

        /// <summary>
        /// Folder that contains *.traineddata files.
        /// </summary>
        private static string GetTessDataFolder()
        {
            if (!string.IsNullOrEmpty(_resolvedTessDataFolder) && Directory.Exists(_resolvedTessDataFolder)
                && Directory.EnumerateFiles(_resolvedTessDataFolder, "*.traineddata").Any())
            {
                return _resolvedTessDataFolder;
            }

            lock (TessDataSync)
            {
                if (!string.IsNullOrEmpty(_resolvedTessDataFolder) && Directory.Exists(_resolvedTessDataFolder)
                    && Directory.EnumerateFiles(_resolvedTessDataFolder, "*.traineddata").Any())
                {
                    return _resolvedTessDataFolder;
                }

                _resolvedTessDataFolder = ResolveAndStageTessDataFolder();
                return _resolvedTessDataFolder;
            }
        }

        /// <summary>
        /// Path passed to TesseractEngine. Tesseract 5 / charlesw 5.2 expects the tessdata
        /// directory itself (the folder that contains *.traineddata), not its parent.
        /// </summary>
        private static string GetTessDataEnginePath() => GetTessDataFolder();

        private static string ResolveAndStageTessDataFolder()
        {
            var packaged = FindPackagedTessDataFolder();
            if (string.IsNullOrEmpty(packaged))
            {
                var fallbackRoot = GetInstallDirectory();
                return Path.GetFullPath(Path.Combine(fallbackRoot, "tessdata"));
            }

            // Store/MSIX package files are read-only; Tesseract may need a writable tessdata tree.
            // Always prefer a LocalAppData stage when the install copy isn't writable.
            if (!IsDirectoryWritable(packaged) || IsRunningPackaged())
            {
                var staged = StageTessDataToAppData(packaged);
                if (!string.IsNullOrEmpty(staged))
                    return staged;
            }

            return packaged;
        }

        private static string? FindPackagedTessDataFolder()
        {
            foreach (var root in GetInstallRoots())
            {
                var tessDataSub = Path.Combine(root, "tessdata");
                if (Directory.Exists(tessDataSub) && HasTrainedData(tessDataSub))
                    return Path.GetFullPath(tessDataSub);

                if (HasTrainedData(root))
                    return Path.GetFullPath(root);
            }

            return null;
        }

        private static bool HasTrainedData(string folder)
        {
            try
            {
                return Directory.EnumerateFiles(folder, "*.traineddata")
                    .Any(path => new FileInfo(path).Length > 100_000);
            }
            catch
            {
                return false;
            }
        }

        private static string? StageTessDataToAppData(string sourceTessDataFolder)
        {
            try
            {
                var destRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SharpShot");
                var destTess = Path.Combine(destRoot, "tessdata");
                Directory.CreateDirectory(destTess);

                foreach (var sourceFile in Directory.EnumerateFiles(sourceTessDataFolder, "*.traineddata"))
                {
                    var destFile = Path.Combine(destTess, Path.GetFileName(sourceFile));
                    var srcInfo = new FileInfo(sourceFile);
                    if (srcInfo.Length < 100_000)
                        continue;

                    var needsCopy = !File.Exists(destFile)
                        || new FileInfo(destFile).Length != srcInfo.Length;
                    if (needsCopy)
                        File.Copy(sourceFile, destFile, overwrite: true);
                }

                return HasTrainedData(destTess) ? Path.GetFullPath(destTess) : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StageTessDataToAppData failed: {ex.Message}");
                return null;
            }
        }

        private static bool IsDirectoryWritable(string folder)
        {
            try
            {
                var probe = Path.Combine(folder, $".sharpshot_write_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRunningPackaged()
        {
            try
            {
                return SharpShot.Utils.PinnedTaskbarIconService.IsRunningPackaged();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Folder that contains SharpShot.exe. Single-file builds extract natives elsewhere, so OCR data lives here.</summary>
        public static string GetInstallDirectory()
        {
            return GetInstallRoots().FirstOrDefault() ?? AppContext.BaseDirectory;
        }

        private static IEnumerable<string> GetInstallRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in new[]
            {
                Environment.ProcessPath is { } exe ? Path.GetDirectoryName(exe) : null,
                AppContext.BaseDirectory,
                AppDomain.CurrentDomain.BaseDirectory
            })
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;
                var full = Path.GetFullPath(candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (seen.Add(full))
                    yield return full;
            }
        }

        /// <summary>
        /// "auto" loads the Latin languages that are installed. A specific code loads only that model.
        /// CJK and Russian stay opt-in because combining them with Latin hurts speed and accuracy.
        /// </summary>
        public static string ResolveTesseractLanguage()
        {
            var selected = "auto";
            try
            {
                var setting = App.SettingsService?.CurrentSettings?.OcrLanguage;
                if (!string.IsNullOrWhiteSpace(setting))
                    selected = setting;
            }
            catch
            {
                // settings not ready yet
            }

            if (selected.Equals("auto", StringComparison.OrdinalIgnoreCase))
                return JoinInstalled(new[] { "eng", "spa", "fra", "deu", "por", "ita", "nld" });

            var folder = GetTessDataFolder();
            if (File.Exists(Path.Combine(folder, selected + ".traineddata")))
                return selected;

            return JoinInstalled(new[] { "eng" });
        }

        private static string JoinInstalled(IEnumerable<string> codes)
        {
            var folder = GetTessDataFolder();
            var present = codes
                .Where(code => File.Exists(Path.Combine(folder, code + ".traineddata")))
                .ToList();
            return present.Count == 0 ? "eng" : string.Join("+", present);
        }

        private static TesseractEngine CreateEngine()
        {
            // Single-file / MSIX: managed code extracts under %TEMP%, but x64\tesseract50.dll lives next to SharpShot.exe.
            try
            {
                TesseractEnviornment.CustomSearchPath = GetInstallDirectory();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CustomSearchPath failed: {ex.Message}");
            }

            // Tesseract 5 wants TESSDATA_PREFIX / datapath to be the tessdata folder itself.
            var dataPath = GetTessDataEnginePath();
            try
            {
                Environment.SetEnvironmentVariable("TESSDATA_PREFIX", dataPath);
            }
            catch
            {
                // ignore
            }

            return new TesseractEngine(dataPath, ResolveTesseractLanguage(), EngineMode.Default);
        }

        public static bool IsAvailable()
        {
            try
            {
                using var engine = CreateEngine();
                return true;
            }
            catch (Exception ex)
            {
                LastAvailabilityError = ex.Message;
                System.Diagnostics.Debug.WriteLine($"OcrService.IsAvailable: {ex.Message}");
                return false;
            }
        }

        /// <summary>Last CreateEngine failure message (for diagnostics in the unavailable dialog).</summary>
        public static string? LastAvailabilityError { get; private set; }

        /// <summary>True when real *.traineddata files are next to the app (or staged).</summary>
        public static bool HasLanguageData()
        {
            try
            {
                return HasTrainedData(GetTessDataFolder());
            }
            catch
            {
                return false;
            }
        }

        /// <summary>True when tesseract50.dll is next to SharpShot.exe under x64\ or x86\.</summary>
        public static bool HasNativeLibraries()
        {
            try
            {
                var root = GetInstallDirectory();
                return File.Exists(Path.Combine(root, "x64", "tesseract50.dll"))
                    || File.Exists(Path.Combine(root, "x86", "tesseract50.dll"));
            }
            catch
            {
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
        /// Screen-text OCR for Smart Regions. Tiles stay at native resolution (upscaled 2x)
        /// and use SingleBlock + SparseText. PageSegMode.Auto is what produced
        /// "boxClipToRectangle / Empty page" and dropped most of the window.
        /// </summary>
        public static async Task<IReadOnlyList<OcrWordResult>> RecognizeScreenTextAsync(Bitmap source, int horizontalSplit = 1)
        {
            if (source == null || source.Width < 40 || source.Height < 40)
                return Array.Empty<OcrWordResult>();

            var all = new List<OcrWordResult>();
            foreach (var tile in BuildScreenTiles(source.Width, source.Height))
            {
                if (IsNearlyBlank(source, tile))
                    continue;

                Bitmap? crop = null;
                Bitmap? up = null;
                try
                {
                    crop = CropForOcr(source, tile);
                    up = ScaleForOcr(crop, 2);
                    var words = await RecognizePreparedAsync(up, PageIteratorLevel.Word, maxSide: 4096,
                        PageSegMode.SingleBlock, 28f).ConfigureAwait(false);
                    OffsetScaled(words, tile.X, tile.Y, 0.5, all);

                    if (CountLetters(words) < 12)
                    {
                        var sparse = await RecognizePreparedAsync(up, PageIteratorLevel.Word, maxSide: 4096,
                            PageSegMode.SparseText, 28f).ConfigureAwait(false);
                        OffsetScaled(sparse, tile.X, tile.Y, 0.5, all);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OcrService screen tile failed: {ex.Message}");
                }
                finally
                {
                    up?.Dispose();
                    crop?.Dispose();
                }
            }

            return ClusterScreenWords(DedupWords(all), horizontalSplit);
        }

        private static List<Rectangle> BuildScreenTiles(int width, int height)
        {
            var tiles = new List<Rectangle>();
            int tileW = width <= 900 ? width : 720;
            int tileH = height <= 700 ? height : 540;
            int stepX = Math.Max(1, tileW - 80);
            int stepY = Math.Max(1, tileH - 60);
            for (int y = 0; y < height; y += stepY)
            {
                int h = Math.Min(tileH, height - y);
                if (h < 36) break;
                for (int x = 0; x < width; x += stepX)
                {
                    int w = Math.Min(tileW, width - x);
                    if (w < 36) break;
                    tiles.Add(new Rectangle(x, y, w, h));
                    if (x + w >= width) break;
                }
                if (y + h >= height) break;
            }
            if (tiles.Count == 0)
                tiles.Add(new Rectangle(0, 0, width, height));
            return tiles;
        }

        private static bool IsNearlyBlank(Bitmap bmp, Rectangle tile)
        {
            try
            {
                int samples = 0;
                long sum = 0;
                long sumSq = 0;
                int stepX = Math.Max(4, tile.Width / 12);
                int stepY = Math.Max(4, tile.Height / 12);
                for (int y = tile.Top; y < tile.Bottom; y += stepY)
                {
                    for (int x = tile.Left; x < tile.Right; x += stepX)
                    {
                        var c = bmp.GetPixel(x, y);
                        int lum = (c.R * 30 + c.G * 59 + c.B * 11) / 100;
                        sum += lum;
                        sumSq += lum * lum;
                        samples++;
                    }
                }
                if (samples < 4) return true;
                double mean = sum / (double)samples;
                double variance = (sumSq / (double)samples) - (mean * mean);
                return variance < 12;
            }
            catch
            {
                return false;
            }
        }

        private static Bitmap CropForOcr(Bitmap src, Rectangle tile)
        {
            tile = Rectangle.Intersect(tile, new Rectangle(0, 0, src.Width, src.Height));
            var bmp = new Bitmap(Math.Max(1, tile.Width), Math.Max(1, tile.Height), PixelFormat.Format32bppArgb);
            bmp.SetResolution(96, 96);
            using var g = Graphics.FromImage(bmp);
            g.DrawImage(src, new Rectangle(0, 0, bmp.Width, bmp.Height), tile, GraphicsUnit.Pixel);
            return bmp;
        }

        private static Bitmap ScaleForOcr(Bitmap src, int factor)
        {
            int w = Math.Max(1, src.Width * factor);
            int h = Math.Max(1, src.Height * factor);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            bmp.SetResolution(96, 96);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, w, h);
            return bmp;
        }

        private static void OffsetScaled(IReadOnlyList<OcrWordResult> src, int ox, int oy, double scale, List<OcrWordResult> dest)
        {
            foreach (var r in src)
            {
                dest.Add(new OcrWordResult
                {
                    Text = r.Text,
                    X = ox + r.X * scale,
                    Y = oy + r.Y * scale,
                    Width = Math.Max(1, r.Width * scale),
                    Height = Math.Max(1, r.Height * scale),
                    Confidence = r.Confidence
                });
            }
        }

        private static int CountLetters(IReadOnlyList<OcrWordResult> words)
        {
            int n = 0;
            foreach (var w in words)
            {
                if (string.IsNullOrWhiteSpace(w.Text)) continue;
                foreach (char c in w.Text)
                    if (char.IsLetterOrDigit(c)) n++;
            }
            return n;
        }

        private static List<OcrWordResult> DedupWords(List<OcrWordResult> words)
        {
            var kept = new List<OcrWordResult>();
            foreach (var word in words.OrderByDescending(w => w.Confidence))
            {
                var a = new RectangleF((float)word.X, (float)word.Y, (float)word.Width, (float)word.Height);
                bool dup = false;
                foreach (var existing in kept)
                {
                    var b = new RectangleF((float)existing.X, (float)existing.Y, (float)existing.Width, (float)existing.Height);
                    var inter = RectangleF.Intersect(a, b);
                    if (inter.IsEmpty) continue;
                    float ratio = (inter.Width * inter.Height) / Math.Max(1f, Math.Min(a.Width * a.Height, b.Width * b.Height));
                    if (ratio > 0.6f)
                    {
                        dup = true;
                        break;
                    }
                }
                if (!dup)
                    kept.Add(word);
            }
            return kept;
        }

        private static IReadOnlyList<OcrWordResult> ClusterScreenWords(List<OcrWordResult> words, int horizontalSplit)
        {
            var ordered = words
                .Where(w => !string.IsNullOrWhiteSpace(w.Text) && w.Width >= 2 && w.Height >= 4)
                .OrderBy(w => w.Y)
                .ThenBy(w => w.X)
                .ToList();
            if (ordered.Count == 0)
                return Array.Empty<OcrWordResult>();

            // Group by baseline first. Splitting here used the line's outer edges while words
            // were still arriving, so a long sentence looked like one huge gap.
            var lines = new List<List<OcrWordResult>>();
            foreach (var word in ordered)
            {
                List<OcrWordResult>? line = null;
                foreach (var candidate in lines)
                {
                    var mid = candidate.Average(w => w.Y + w.Height / 2);
                    double wordMid = word.Y + word.Height / 2;
                    double tol = Math.Max(8, candidate.Average(w => w.Height) * 0.6);
                    if (Math.Abs(wordMid - mid) > tol)
                        continue;
                    line = candidate;
                    break;
                }
                if (line == null)
                {
                    line = new List<OcrWordResult>();
                    lines.Add(line);
                }
                line.Add(word);
            }

            var result = new List<OcrWordResult>();
            foreach (var line in lines)
            {
                foreach (var run in SplitBaselineOnAdjacentGaps(line, horizontalSplit))
                    EmitWordRun(result, run);
            }
            return result;
        }

        /// <summary>
        /// Split a finished baseline only where neighboring words have an outlier gap.
        /// A sentence's own word spacing is the reference, so a long line is not treated as one gap.
        /// </summary>
        private static List<List<OcrWordResult>> SplitBaselineOnAdjacentGaps(List<OcrWordResult> line, int sensitivity)
        {
            var ordered = line.OrderBy(w => w.X).ThenBy(w => w.Y).ToList();
            if (sensitivity <= 0 || ordered.Count < 2)
                return new List<List<OcrWordResult>> { ordered };

            var gaps = new double[ordered.Count - 1];
            for (int i = 0; i < gaps.Length; i++)
            {
                double gap = ordered[i + 1].X - (ordered[i].X + ordered[i].Width);
                gaps[i] = Math.Max(0, gap);
            }

            double height = Math.Max(10, ordered.Average(w => w.Height));
            var wordLike = gaps.Where(g => g <= height * 1.25).ToList();
            double typical = wordLike.Count > 0 ? Median(wordLike) : Math.Max(4, height * 0.25);
            double threshold = AdjacentGapThreshold(sensitivity, typical, height);

            var runs = new List<List<OcrWordResult>>();
            var run = new List<OcrWordResult> { ordered[0] };
            for (int i = 1; i < ordered.Count; i++)
            {
                if (gaps[i - 1] > threshold)
                {
                    runs.Add(run);
                    run = new List<OcrWordResult>();
                }
                run.Add(ordered[i]);
            }
            runs.Add(run);
            return runs;
        }

        /// <summary>
        /// 1 is the smallest split, just right of Off. Higher values split on smaller holes.
        /// </summary>
        private static double AdjacentGapThreshold(int sensitivity, double typicalWordGap, double lineHeight)
        {
            (double typicalMult, double heightMult, double floor) = Math.Clamp(sensitivity, 1, 5) switch
            {
                1 => (16.0, 10.0, 280),
                2 => (10.0, 7.0, 180),
                3 => (6.0, 4.5, 110),
                4 => (3.8, 2.8, 64),
                _ => (2.4, 1.8, 36)
            };
            return Math.Max(floor, Math.Max(typicalWordGap * typicalMult, lineHeight * heightMult));
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            var ordered = values.OrderBy(v => v).ToList();
            int mid = ordered.Count / 2;
            if (ordered.Count % 2 == 1) return ordered[mid];
            return (ordered[mid - 1] + ordered[mid]) / 2;
        }

        private static void EmitWordRun(List<OcrWordResult> result, List<OcrWordResult> line)
        {
            if (line.Count == 0) return;
            double x1 = line.Min(w => w.X);
            double y1 = line.Min(w => w.Y);
            double x2 = line.Max(w => w.X + w.Width);
            double y2 = line.Max(w => w.Y + w.Height);
            var text = string.Join(" ", line.OrderBy(w => w.X).Select(w => w.Text).Where(t => !string.IsNullOrWhiteSpace(t))).Trim();
            if (text.Length == 0) return;
            result.Add(new OcrWordResult
            {
                Text = text,
                X = x1,
                Y = y1,
                Width = Math.Max(1, x2 - x1),
                Height = Math.Max(1, y2 - y1),
                Confidence = line.Average(w => w.Confidence)
            });
        }

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

                    using var engine = CreateEngine();
                    engine.SetVariable("user_defined_dpi", "96");
                    engine.SetVariable("tessedit_do_invert", "1");
                    toProcess.SetResolution(96, 96);
                    using var page = engine.Process(toProcess, segMode);
                    using var iter = page.GetIterator();
                    iter.Begin();
                    do
                    {
                        if (!iter.TryGetBoundingBox(level, out var rect))
                            continue;
                        if (rect.X1 < -2 || rect.Y1 < -2 || rect.X2 > w + 2 || rect.Y2 > h + 2)
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
