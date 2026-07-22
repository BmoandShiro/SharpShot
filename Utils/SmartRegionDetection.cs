using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using SharpShot.Services;

namespace SharpShot.Utils
{
    /// <summary>
    /// Detects visible content regions for smart capture/OCR highlights.
    /// Combines UI Automation (great for Explorer/Win32), TextPattern (consoles),
    /// filtered point sampling (Chromium), and OCR word clustering when the tree is sparse.
    /// Note: Windows Snipping Tool "Perfect Screenshot" uses on-device NPU AI — we approximate
    /// with accessibility + OCR, which works on any PC.
    /// </summary>
    public static class SmartRegionDetection
    {
        private const int MinWidth = 16;
        private const int MinHeight = 10;
        private const int MaxRects = 180;
        private const int MaxWalkNodes = 600;
        private const int MaxDepth = 14;
        private const double MaxWindowCoverage = 0.82;
        private const int UiaSparseThreshold = 6;
        private const uint GA_ROOT = 2;
        private const uint PW_CLIENTONLY = 0x1;
        private const uint PW_RENDERFULLCONTENT = 0x2;

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        private const int SRCCOPY = 0x00CC0020;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Picks a useful target HWND for smart regions / region select.
        /// Prefers the most recently clicked or activated non-SharpShot window, then
        /// cursor / hint / foreground fallbacks.
        /// </summary>
        public static IntPtr ResolveTargetWindow(IntPtr preferredHint = default)
        {
            uint ourPid = (uint)Process.GetCurrentProcess().Id;

            var last = LastExternalWindowTracker.GetLastWindow();
            if (last != IntPtr.Zero && IsExternalVisibleWindow(last, ourPid))
                return last;

            if (GetCursorPos(out POINT pt))
            {
                var underCursor = WindowFromPoint(pt);
                if (underCursor != IntPtr.Zero)
                {
                    var root = GetAncestor(underCursor, GA_ROOT);
                    if (root == IntPtr.Zero) root = underCursor;
                    if (IsExternalVisibleWindow(root, ourPid))
                        return root;
                }
            }

            if (preferredHint != IntPtr.Zero && IsExternalVisibleWindow(preferredHint, ourPid))
            {
                var hintRoot = GetAncestor(preferredHint, GA_ROOT);
                return hintRoot != IntPtr.Zero ? hintRoot : preferredHint;
            }

            var foreground = GetForegroundWindow();
            if (foreground != IntPtr.Zero && IsExternalVisibleWindow(foreground, ourPid))
                return foreground;

            return FindTopExternalWindowNearCursor(ourPid);
        }

        private static IntPtr FindTopExternalWindowNearCursor(uint ourPid)
        {
            GetCursorPos(out POINT pt);
            IntPtr best = IntPtr.Zero;
            int bestArea = int.MaxValue;

            EnumWindows((hWnd, _) =>
            {
                if (!IsExternalVisibleWindow(hWnd, ourPid) || IsIconic(hWnd))
                    return true;
                if (!GetWindowRect(hWnd, out RECT rc))
                    return true;

                int width = rc.Right - rc.Left;
                int height = rc.Bottom - rc.Top;
                if (width < 100 || height < 100)
                    return true;

                bool containsCursor = pt.X >= rc.Left && pt.X < rc.Right && pt.Y >= rc.Top && pt.Y < rc.Bottom;
                if (!containsCursor)
                    return true;

                int area = width * height;
                if (best == IntPtr.Zero || area < bestArea)
                {
                    best = hWnd;
                    bestArea = area;
                }
                return true;
            }, IntPtr.Zero);

            return best;
        }

        private static bool IsExternalVisibleWindow(IntPtr hwnd, uint ourPid)
        {
            if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd))
                return false;
            GetWindowThreadProcessId(hwnd, out uint pid);
            return pid != 0 && pid != ourPid;
        }

        /// <summary>
        /// Fast path: UI Automation + TextPattern + filtered point sampling (no OCR).
        /// </summary>
        /// <summary>
        /// Fast sync path. Intentionally empty — the screenshots showed UIA chrome
        /// (sidebar labels, close buttons, notification dots) dominating before OCR finished.
        /// Callers should use <see cref="GetDetectedRegionsAsync"/>.
        /// </summary>
        public static List<Rectangle> GetDetectedRegions(IntPtr windowHandle)
        {
            return new List<Rectangle>();
        }

        /// <summary>
        /// OCR text-lines are primary for all apps. UIA is only a light supplement for large images/controls.
        /// Optional <paramref name="screenSnapshot"/> (e.g. region-select freeze frame) avoids capturing through overlays.
        /// </summary>
        public static async Task<List<Rectangle>> GetDetectedRegionsAsync(
            IntPtr windowHandle,
            Bitmap? screenSnapshot = null,
            Rectangle screenSnapshotBounds = default,
            bool denseOcr = true)
        {
            if (windowHandle == IntPtr.Zero)
                return new List<Rectangle>();

            var ocrRects = new List<Rectangle>();
            try
            {
                ocrRects = await CollectOcrRegionsAsync(windowHandle, screenSnapshot, screenSnapshotBounds, denseOcr)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SmartRegion OCR enrich: {ex.Message}");
            }

            // Light UIA supplement only — never let tiny chrome dominate when OCR found real text.
            var uia = CollectUsefulUiaSupplement(windowHandle);

            if (ocrRects.Count > 0)
            {
                var merged = new List<Rectangle>(ocrRects.Count + Math.Min(uia.Count, 20));
                merged.AddRange(ocrRects);
                // Only keep large UIA regions (images/cards) that don't swamp text lines
                foreach (var r in uia)
                {
                    if (r.Width >= 80 && r.Height >= 48 && Area(r) >= 80 * 48)
                        merged.Add(r);
                }
                return DedupAndCap(merged);
            }

            // OCR failed — filtered UIA is better than nothing (Explorer etc.)
            return DedupAndCap(uia);
        }

        /// <summary>
        /// UIA regions worth keeping as supplements — excludes tiny leaves / scrollbar chrome.
        /// </summary>
        private static List<Rectangle> CollectUsefulUiaSupplement(IntPtr windowHandle)
        {
            // Chromium/Electron/consoles: UIA chrome is what the user's screenshots showed
            // (sidebar labels, address bar bands). OCR owns those surfaces.
            if (LooksLikeBrowserOrConsole(windowHandle))
                return new List<Rectangle>();

            var raw = CollectUiaRegions(windowHandle);
            var filtered = new List<Rectangle>();
            foreach (var r in raw)
            {
                if (!IsPlausibleContentRect(r)) continue;
                if (r.Width < 36 || r.Height < 14) continue;
                if (r.Width * r.Height < 36 * 16) continue;
                if (IsSkinnyChrome(r.Width, r.Height)) continue;
                filtered.Add(r);
            }
            return filtered;
        }

        private static List<Rectangle> CollectUiaRegions(IntPtr windowHandle)
        {
            var rects = new List<Rectangle>();

            CollectFromHandle(windowHandle, rects);

            foreach (var child in FindInterestingChildHwnds(windowHandle))
                CollectFromHandle(child, rects);

            CollectTextPatternRanges(windowHandle, rects);

            // Point-sampling disabled: it was the main source of scrollbar / misaligned chrome boxes.
            return rects;
        }

        private static List<Rectangle> DedupAndCap(List<Rectangle> rects)
        {
            var result = new List<Rectangle>();
            rects.Sort((a, b) => (a.Width * a.Height).CompareTo(b.Width * b.Height));

            foreach (var r in rects)
            {
                if (result.Count >= MaxRects) break;
                if (!IsPlausibleContentRect(r)) continue;

                bool redundant = false;
                foreach (var existing in result)
                {
                    if (OverlapRatio(r, existing) > 0.85 && Area(r) <= Area(existing) * 1.2)
                    {
                        redundant = true;
                        break;
                    }
                    if (RectangleContains(r, existing) && Area(r) > Area(existing) * 1.5)
                    {
                        redundant = true;
                        break;
                    }
                }
                if (!redundant)
                    result.Add(r);
            }

            Debug.WriteLine($"SmartRegionDetection: {result.Count} regions");
            return result;
        }

        private static void CollectFromHandle(IntPtr hwnd, List<Rectangle> rects)
        {
            try
            {
                var element = AutomationElement.FromHandle(hwnd);
                if (element == null) return;

                try
                {
                    _ = element.Current.Name;
                    _ = element.Current.FrameworkId;
                    _ = element.Current.ControlType;
                }
                catch { /* ignore */ }

                var windowBounds = element.Current.BoundingRectangle;
                double windowArea = windowBounds.IsEmpty
                    ? 0
                    : Math.Max(1, windowBounds.Width * windowBounds.Height);

                int visited = 0;
                CollectElementRects(element, rects, windowArea, depth: 0, ref visited, TreeWalker.RawViewWalker);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CollectFromHandle: {ex.Message}");
            }
        }

        /// <summary>
        /// Consoles and some editors expose TextPattern with visible-range bounding boxes —
        /// far better than walking leaf Text nodes (which CMD often lacks).
        /// </summary>
        private static void CollectTextPatternRanges(IntPtr hwnd, List<Rectangle> rects)
        {
            try
            {
                var root = AutomationElement.FromHandle(hwnd);
                if (root == null) return;

                var condition = new PropertyCondition(AutomationElement.IsTextPatternAvailableProperty, true);
                AutomationElementCollection? matches = null;
                try
                {
                    matches = root.FindAll(TreeScope.Descendants, condition);
                }
                catch
                {
                    // Some hosts throw on FindAll; try the root itself.
                }

                void TryAddFromElement(AutomationElement el)
                {
                    if (el == null) return;
                    if (!el.TryGetCurrentPattern(TextPattern.Pattern, out object? patternObj) || patternObj is not TextPattern tp)
                        return;

                    TextPatternRange[] ranges;
                    try { ranges = tp.GetVisibleRanges(); }
                    catch { return; }

                    foreach (var range in ranges)
                    {
                        System.Windows.Rect[] boxes;
                        try { boxes = range.GetBoundingRectangles(); }
                        catch { continue; }

                        foreach (var box in boxes)
                        {
                            if (box.IsEmpty) continue;
                            var r = new Rectangle(
                                (int)Math.Round(box.X),
                                (int)Math.Round(box.Y),
                                (int)Math.Round(box.Width),
                                (int)Math.Round(box.Height));
                            if (IsPlausibleContentRect(r))
                                rects.Add(r);
                        }
                    }
                }

                if (matches != null)
                {
                    int n = Math.Min(matches.Count, 8);
                    for (int i = 0; i < n; i++)
                        TryAddFromElement(matches[i]);
                }
                else
                {
                    TryAddFromElement(root);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CollectTextPatternRanges: {ex.Message}");
            }
        }

        private static async Task<List<Rectangle>> CollectOcrRegionsAsync(
            IntPtr hwnd,
            Bitmap? screenSnapshot = null,
            Rectangle screenSnapshotBounds = default,
            bool denseOcr = true)
        {
            var result = new List<Rectangle>();
            if (!OcrService.IsAvailable())
                return result;
            if (!TryCaptureClientBitmap(hwnd, out var bmp, out int originX, out int originY,
                    screenSnapshot, screenSnapshotBounds))
                return result;

            try
            {
                IReadOnlyList<OcrWordResult> lines = denseOcr
                    ? await OcrService.RecognizeSmartRegionLinesAsync(bmp).ConfigureAwait(false)
                    : await OcrService.RecognizeTextLinesAsync(bmp).ConfigureAwait(false);

                if (lines.Count == 0)
                {
                    var words = await OcrService.RecognizeWordsAsync(bmp).ConfigureAwait(false);
                    var clustered = ClusterWordsIntoLines(words);
                    foreach (var line in clustered)
                    {
                        if (IsGhostImageRect(line, bmp.Width, bmp.Height)) continue;
                        TryAddImageRect(result, originX, originY, line);
                    }
                    return result;
                }

                float minConf = denseOcr ? 45f : OcrService.DefaultMinConfidence;
                var lineRects = new List<RectangleF>();
                foreach (var line in lines)
                {
                    if (!TryAcceptOcrLine(line, bmp.Width, bmp.Height, out var rf, minConf))
                        continue;
                    lineRects.Add(rf);
                    TryAddImageRect(result, originX, originY, rf);
                }

                // Paragraph blocks for multi-line clusters (bullet lists, blurbs) — keep lines too.
                foreach (var block in MergeLinesIntoBlocks(lineRects))
                {
                    if (block.Height < 36) continue;
                    if (IsGhostImageRect(block, bmp.Width, bmp.Height)) continue;
                    TryAddImageRect(result, originX, originY, block, pad: 3);
                }
            }
            finally
            {
                bmp.Dispose();
            }

            return result;
        }

        private static bool TryAcceptOcrLine(
            OcrWordResult line, int imageW, int imageH, out RectangleF rf,
            float minConfidence = OcrService.DefaultMinConfidence)
        {
            rf = default;
            if (string.IsNullOrWhiteSpace(line.Text)) return false;
            var trimmed = line.Text.Trim();
            if (IsLikelyGlyphNoise(trimmed)) return false;
            if (line.Confidence > 0 && line.Confidence < minConfidence) return false;
            if (line.Width < 14 || line.Height < 8) return false;
            if (trimmed.Length <= 1) return false;
            if (trimmed.Length <= 2 && !char.IsLetterOrDigit(trimmed[0])) return false;

            rf = new RectangleF((float)line.X, (float)line.Y, (float)line.Width, (float)line.Height);
            if (IsGhostImageRect(rf, imageW, imageH)) return false;
            return true;
        }

        /// <summary>
        /// Reject phantom boxes: tiny strips hugging image edges with almost no text mass.
        /// </summary>
        private static bool IsGhostImageRect(RectangleF r, int imageW, int imageH)
        {
            if (imageW <= 0 || imageH <= 0) return false;

            if (r.Height <= 10 && r.Width < imageW * 0.35f) return true;
            if (r.Width <= 10 && r.Height < imageH * 0.35f) return true;

            const int edge = 12;
            bool nearBottom = r.Y >= imageH - edge - r.Height;
            bool nearTop = r.Bottom <= edge + r.Height && r.Y <= edge;
            bool nearLeft = r.Right <= edge + r.Width && r.X <= edge;
            bool nearRight = r.X >= imageW - edge - r.Width;

            float area = r.Width * r.Height;
            if (area < 900 && (nearBottom || nearTop || nearLeft || nearRight))
                return true;

            if ((nearBottom || nearTop) && r.Height <= 14 && r.Width < imageW * 0.5f)
                return true;

            return false;
        }

        private static void TryAddImageRect(List<Rectangle> result, int originX, int originY, RectangleF img, int pad = 2)
        {
            var r = new Rectangle(
                originX + (int)Math.Floor(img.X) - pad,
                originY + (int)Math.Floor(img.Y) - pad,
                (int)Math.Ceiling(img.Width) + pad * 2,
                (int)Math.Ceiling(img.Height) + pad * 2);
            if (IsPlausibleContentRect(r))
                result.Add(r);
        }

        /// <summary>
        /// Capture the window client area. Prefer cropping from an existing screen snapshot (freeze frame)
        /// so region-select OCR matches what the user sees and never includes our overlay.
        /// </summary>
        private static bool TryCaptureClientBitmap(
            IntPtr hwnd,
            out Bitmap bitmap,
            out int originX,
            out int originY,
            Bitmap? screenSnapshot = null,
            Rectangle screenSnapshotBounds = default)
        {
            bitmap = null!;
            originX = originY = 0;
            if (!GetClientRect(hwnd, out RECT client) || client.Right < 40 || client.Bottom < 40)
                return false;
            if (!GetWindowRect(hwnd, out RECT windowRect))
                return false;

            var topLeft = new POINT { X = 0, Y = 0 };
            if (!ClientToScreen(hwnd, ref topLeft))
                return false;

            int insetX = Math.Min(4, client.Right / 80);
            int insetTop = Math.Min(4, client.Bottom / 40);
            int insetBottom = Math.Min(4, client.Bottom / 80);
            int w = client.Right - insetX * 2;
            int h = client.Bottom - insetTop - insetBottom;
            if (w < 80 || h < 60) return false;

            originX = topLeft.X + insetX;
            originY = topLeft.Y + insetTop;

            // 1) Crop from freeze / full-desktop snapshot when available
            if (screenSnapshot != null && screenSnapshotBounds.Width > 0 && screenSnapshotBounds.Height > 0)
            {
                try
                {
                    int sx = originX - screenSnapshotBounds.X;
                    int sy = originY - screenSnapshotBounds.Y;
                    var crop = Rectangle.Intersect(
                        new Rectangle(sx, sy, w, h),
                        new Rectangle(0, 0, screenSnapshot.Width, screenSnapshot.Height));
                    if (crop.Width >= 80 && crop.Height >= 60)
                    {
                        bitmap = screenSnapshot.Clone(crop, PixelFormat.Format24bppRgb);
                        originX = screenSnapshotBounds.X + crop.X;
                        originY = screenSnapshotBounds.Y + crop.Y;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Freeze crop failed: {ex.Message}");
                }
            }

            int clientOffsetX = topLeft.X - windowRect.Left + insetX;
            int clientOffsetY = topLeft.Y - windowRect.Top + insetTop;
            int winW = windowRect.Right - windowRect.Left;
            int winH = windowRect.Bottom - windowRect.Top;
            if (winW < 80 || winH < 60) return false;

            Bitmap? full = null;
            try
            {
                full = new Bitmap(winW, winH, PixelFormat.Format24bppRgb);
                bool printed = false;
                using (var g = Graphics.FromImage(full))
                {
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        printed = PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT);
                        if (!printed)
                            printed = PrintWindow(hwnd, hdc, 0);
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc);
                    }
                }

                if (!printed || IsMostlyBlack(full))
                {
                    if (!TryBitBltWindow(hwnd, full) || IsMostlyBlack(full))
                    {
                        full.Dispose();
                        full = null;
                        bitmap = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                        using var g2 = Graphics.FromImage(bitmap);
                        g2.CopyFromScreen(originX, originY, 0, 0, new Size(w, h));
                        return true;
                    }
                }

                var cloneRect = Rectangle.Intersect(
                    new Rectangle(clientOffsetX, clientOffsetY, w, h),
                    new Rectangle(0, 0, full.Width, full.Height));
                if (cloneRect.Width < 80 || cloneRect.Height < 60)
                {
                    full.Dispose();
                    return false;
                }

                bitmap = full.Clone(cloneRect, PixelFormat.Format24bppRgb);
                full.Dispose();
                full = null;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryCaptureClientBitmap: {ex.Message}");
                full?.Dispose();
                bitmap?.Dispose();
                bitmap = null!;
                return false;
            }
        }

        private static bool TryBitBltWindow(IntPtr hwnd, Bitmap dest)
        {
            IntPtr hdcSrc = GetWindowDC(hwnd);
            if (hdcSrc == IntPtr.Zero) return false;
            try
            {
                using var g = Graphics.FromImage(dest);
                IntPtr hdcDst = g.GetHdc();
                try
                {
                    return BitBlt(hdcDst, 0, 0, dest.Width, dest.Height, hdcSrc, 0, 0, SRCCOPY);
                }
                finally
                {
                    g.ReleaseHdc(hdcDst);
                }
            }
            finally
            {
                ReleaseDC(hwnd, hdcSrc);
            }
        }

        private static bool IsMostlyBlack(Bitmap bmp)
        {
            try
            {
                int w = bmp.Width;
                int h = bmp.Height;
                int samples = 0;
                int dark = 0;
                for (int y = h / 8; y < h; y += Math.Max(1, h / 6))
                {
                    for (int x = w / 8; x < w; x += Math.Max(1, w / 6))
                    {
                        var c = bmp.GetPixel(x, y);
                        samples++;
                        if (c.R + c.G + c.B < 30) dark++;
                    }
                }
                return samples > 0 && dark > samples * 0.85;
            }
            catch
            {
                return false;
            }
        }

        private static List<RectangleF> ClusterWordsIntoLines(IReadOnlyList<OcrWordResult> words)
        {
            var ordered = words
                .Where(w => !string.IsNullOrWhiteSpace(w.Text) && w.Width >= 2 && w.Height >= 4)
                .Where(w => !IsLikelyGlyphNoise(w.Text))
                .OrderBy(w => w.Y)
                .ThenBy(w => w.X)
                .ToList();

            var lines = new List<List<OcrWordResult>>();
            foreach (var w in ordered)
            {
                bool placed = false;
                foreach (var line in lines)
                {
                    double lineCy = line.Average(x => x.Y + x.Height / 2);
                    double wordCy = w.Y + w.Height / 2;
                    double tol = Math.Max(8, line.Average(x => x.Height) * 0.55);
                    if (Math.Abs(wordCy - lineCy) <= tol)
                    {
                        line.Add(w);
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                    lines.Add(new List<OcrWordResult> { w });
            }

            var rects = new List<RectangleF>();
            foreach (var line in lines)
            {
                if (line.Count == 0) continue;
                float x1 = (float)line.Min(w => w.X);
                float y1 = (float)line.Min(w => w.Y);
                float x2 = (float)line.Max(w => w.X + w.Width);
                float y2 = (float)line.Max(w => w.Y + w.Height);
                // Skip tiny single-glyph "lines"
                if (x2 - x1 < 18 || y2 - y1 < 8) continue;
                if (line.Count == 1 && line[0].Text.Length <= 1) continue;
                rects.Add(RectangleF.FromLTRB(x1, y1, x2, y2));
            }
            return rects;
        }

        private static List<RectangleF> MergeLinesIntoBlocks(List<RectangleF> lines)
        {
            if (lines.Count == 0) return lines;

            var sorted = lines.OrderBy(l => l.Y).ThenBy(l => l.X).ToList();
            var blocks = new List<RectangleF>();
            var current = sorted[0];
            int linesInBlock = 1;

            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];
                float gap = next.Y - current.Bottom;
                float avgH = Math.Max(8f, (current.Height / Math.Max(1, linesInBlock) + next.Height) / 2f);

                // Similar left edges OR substantial horizontal overlap (bullet lists / paragraphs)
                float leftDelta = Math.Abs(next.X - current.X);
                bool alignedLeft = leftDelta <= Math.Max(24f, avgH * 1.2f);
                bool overlappingX = next.Right >= current.X - 24 && next.X <= current.Right + 24;
                // Cap merge so we don't swallow an entire page: stop after ~8 lines or large height
                bool withinSize = linesInBlock < 8 && current.Height < avgH * 10f;

                if (gap >= -4 && gap <= avgH * 1.55f && (alignedLeft || overlappingX) && withinSize)
                {
                    current = RectangleF.Union(current, next);
                    linesInBlock++;
                }
                else
                {
                    if (linesInBlock >= 2 && current.Width >= 40 && current.Height >= 20)
                        blocks.Add(current);
                    current = next;
                    linesInBlock = 1;
                }
            }
            if (linesInBlock >= 2 && current.Width >= 40 && current.Height >= 20)
                blocks.Add(current);

            return blocks;
        }

        private static bool IsLikelyGlyphNoise(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            text = text.Trim();
            if (text.Length == 1)
            {
                char c = text[0];
                if (!char.IsLetterOrDigit(c) && c != '$' && c != '#' && c != '@' && c != '%' && c != '&')
                    return true;
            }
            if (text.Length <= 2 && "|_/\\•·▪▫◆◇○●□■▲▼◀▶‹›«»—–…".Contains(text))
                return true;
            // Pure punctuation / symbol runs
            if (text.Length <= 4 && text.All(c => !char.IsLetterOrDigit(c)))
                return true;
            return false;
        }

        private static List<IntPtr> FindInterestingChildHwnds(IntPtr root)
        {
            var list = new List<IntPtr>();
            try
            {
                EnumChildWindows(root, (hWnd, _) =>
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    var cls = GetWindowClass(hWnd);
                    if (cls.IndexOf("Chrome_RenderWidgetHostHWND", StringComparison.OrdinalIgnoreCase) >= 0
                        || cls.IndexOf("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) >= 0
                        || cls.IndexOf("Intermediate D3D Window", StringComparison.OrdinalIgnoreCase) >= 0
                        || cls.IndexOf("MozillaWindowClass", StringComparison.OrdinalIgnoreCase) >= 0
                        || cls.IndexOf("MozillaCompositorWindowClass", StringComparison.OrdinalIgnoreCase) >= 0
                        || cls.Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase)
                        || cls.IndexOf("CASCADIA_HOSTING_WINDOW_CLASS", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (GetWindowRect(hWnd, out RECT rc))
                        {
                            int w = rc.Right - rc.Left;
                            int h = rc.Bottom - rc.Top;
                            if (w >= 80 && h >= 60)
                                list.Add(hWnd);
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FindInterestingChildHwnds: {ex.Message}");
            }
            return list;
        }

        private static bool LooksLikeBrowserOrConsole(IntPtr hwnd)
        {
            var cls = GetWindowClass(hwnd);
            if (cls.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0
                || cls.IndexOf("Mozilla", StringComparison.OrdinalIgnoreCase) >= 0
                || cls.Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase)
                || cls.IndexOf("CASCADIA", StringComparison.OrdinalIgnoreCase) >= 0
                || cls.IndexOf("Opera", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            foreach (var child in FindInterestingChildHwnds(hwnd))
            {
                var c = GetWindowClass(child);
                if (c.IndexOf("Chrome_RenderWidgetHostHWND", StringComparison.OrdinalIgnoreCase) >= 0
                    || c.Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string GetWindowClass(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static bool GetWindowScreenBounds(IntPtr hwnd, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            if (!GetClientRect(hwnd, out RECT client) || client.Right <= 0 || client.Bottom <= 0)
            {
                if (!GetWindowRect(hwnd, out RECT wr))
                    return false;
                bounds = new Rectangle(wr.Left, wr.Top, wr.Right - wr.Left, wr.Bottom - wr.Top);
                return bounds.Width > 0 && bounds.Height > 0;
            }

            var topLeft = new POINT { X = client.Left, Y = client.Top };
            if (!ClientToScreen(hwnd, ref topLeft))
                return false;

            int insetX = Math.Min(40, client.Right / 10);
            int insetY = Math.Min(80, client.Bottom / 8);
            int w = client.Right - insetX * 2;
            int h = client.Bottom - insetY - insetX;
            if (w < 80 || h < 80) return false;

            bounds = new Rectangle(topLeft.X + insetX, topLeft.Y + insetY, w, h);
            return true;
        }

        private static void CollectFromPointSampling(Rectangle area, List<Rectangle> rects, bool denser)
        {
            int step = denser ? 48 : 72;
            var seen = new HashSet<string>();

            for (int y = area.Y + step / 2; y < area.Bottom; y += step)
            {
                for (int x = area.X + step / 2; x < area.Right; x += step)
                {
                    if (rects.Count >= MaxRects * 2) return;
                    try
                    {
                        var el = AutomationElement.FromPoint(new System.Windows.Point(x, y));
                        if (el == null) continue;
                        if (IsNoiseControlType(el.Current.ControlType))
                            continue;

                        var candidate = el;
                        try
                        {
                            var walker = TreeWalker.ControlViewWalker;
                            for (int i = 0; i < 4; i++)
                            {
                                var ct = candidate.Current.ControlType;
                                if (IsNoiseControlType(ct))
                                {
                                    var up = walker.GetParent(candidate);
                                    if (up == null) break;
                                    candidate = up;
                                    continue;
                                }

                                var b = candidate.Current.BoundingRectangle;
                                if (b.IsEmpty) break;
                                if (IsInterestingControlType(ct)
                                    && b.Width >= 40 && b.Height >= 16
                                    && b.Width <= area.Width * 0.9
                                    && b.Height <= area.Height * 0.9
                                    && !IsSkinnyChrome(b.Width, b.Height))
                                    break;

                                var parent = walker.GetParent(candidate);
                                if (parent == null) break;
                                candidate = parent;
                            }
                        }
                        catch { /* use leaf */ }

                        if (IsNoiseControlType(candidate.Current.ControlType))
                            continue;
                        if (!IsInterestingControlType(candidate.Current.ControlType))
                            continue;

                        var bounds = candidate.Current.BoundingRectangle;
                        if (bounds.IsEmpty) continue;
                        if (IsSkinnyChrome(bounds.Width, bounds.Height))
                            continue;
                        if (bounds.Width < MinWidth || bounds.Height < MinHeight)
                            continue;
                        if (bounds.Width > area.Width * MaxWindowCoverage && bounds.Height > area.Height * MaxWindowCoverage)
                            continue;

                        var r = new Rectangle(
                            (int)Math.Round(bounds.X),
                            (int)Math.Round(bounds.Y),
                            (int)Math.Round(bounds.Width),
                            (int)Math.Round(bounds.Height));

                        string key = $"{r.X},{r.Y},{r.Width},{r.Height}";
                        if (seen.Add(key))
                            rects.Add(r);
                    }
                    catch
                    {
                        // FromPoint can fail on protected / empty spots
                    }
                }
            }
        }

        public static Rectangle? GetSmallestRegionAtPoint(IReadOnlyList<Rectangle> rects, int screenX, int screenY)
        {
            Rectangle? best = null;
            long bestArea = long.MaxValue;
            foreach (var r in rects)
            {
                if (screenX < r.X || screenX >= r.Right || screenY < r.Y || screenY >= r.Bottom)
                    continue;
                long area = Area(r);
                if (area < bestArea)
                {
                    bestArea = area;
                    best = r;
                }
            }
            return best;
        }

        private static void CollectElementRects(
            AutomationElement element,
            List<Rectangle> rects,
            double windowArea,
            int depth,
            ref int visited,
            TreeWalker walker)
        {
            if (visited >= MaxWalkNodes || rects.Count >= MaxRects * 2)
                return;

            try
            {
                visited++;

                var ct = element.Current.ControlType;
                if (IsNoiseControlType(ct))
                {
                    // Still walk children of some containers, but skip scrollbar subtrees entirely
                    if (ct.Id == ControlType.ScrollBar.Id || ct.Id == ControlType.Thumb.Id
                        || ct.Id == ControlType.Separator.Id || ct.Id == ControlType.TitleBar.Id)
                        return;
                }
                else
                {
                    var bounds = element.Current.BoundingRectangle;
                    if (!bounds.IsEmpty && bounds.Width >= MinWidth && bounds.Height >= MinHeight
                        && !IsSkinnyChrome(bounds.Width, bounds.Height)
                        && IsInterestingControlType(ct))
                    {
                        double area = bounds.Width * bounds.Height;
                        double coverage = windowArea > 0 ? area / windowArea : 0;
                        if (coverage <= MaxWindowCoverage)
                        {
                            if (!IsBroadContainer(ct) || LooksLikeContentBlock(bounds, windowArea, ct))
                            {
                                rects.Add(new Rectangle(
                                    (int)Math.Round(bounds.X),
                                    (int)Math.Round(bounds.Y),
                                    (int)Math.Round(bounds.Width),
                                    (int)Math.Round(bounds.Height)));
                            }
                        }
                    }
                }

                if (depth >= MaxDepth)
                    return;

                var child = walker.GetFirstChild(element);
                while (child != null)
                {
                    if (visited >= MaxWalkNodes || rects.Count >= MaxRects * 2)
                        break;
                    CollectElementRects(child, rects, windowArea, depth + 1, ref visited, walker);
                    child = walker.GetNextSibling(child);
                }
            }
            catch
            {
                // Ignore per-element errors (stale UIA nodes, etc.)
            }
        }

        private static bool IsNoiseControlType(ControlType? controlType)
        {
            if (controlType == null) return true;
            int id = controlType.Id;
            return id == ControlType.ScrollBar.Id
                   || id == ControlType.Thumb.Id
                   || id == ControlType.Separator.Id
                   || id == ControlType.TitleBar.Id
                   || id == ControlType.MenuBar.Id
                   || id == ControlType.ToolBar.Id
                   || id == ControlType.StatusBar.Id
                   || id == ControlType.Slider.Id
                   || id == ControlType.ProgressBar.Id
                   || id == ControlType.Spinner.Id
                   || id == ControlType.SplitButton.Id;
        }

        private static bool IsSkinnyChrome(double width, double height)
        {
            if (width < 1 || height < 1) return true;
            // Classic vertical/horizontal scrollbars and thin splitter chrome
            if (width <= 22 && height >= 80) return true;
            if (height <= 22 && width >= 80) return true;
            double ratio = width / height;
            return ratio >= 18 || ratio <= 1.0 / 18.0;
        }

        private static bool IsPlausibleContentRect(Rectangle r)
        {
            if (r.Width < MinWidth || r.Height < MinHeight) return false;
            if (IsSkinnyChrome(r.Width, r.Height)) return false;
            if (r.Width > 8000 || r.Height > 8000) return false;
            return true;
        }

        private static bool IsInterestingControlType(ControlType controlType)
        {
            if (controlType == null) return false;
            int id = controlType.Id;
            return id == ControlType.Image.Id
                   || id == ControlType.Document.Id
                   || id == ControlType.Text.Id
                   || id == ControlType.List.Id
                   || id == ControlType.ListItem.Id
                   || id == ControlType.Table.Id
                   || id == ControlType.DataGrid.Id
                   || id == ControlType.DataItem.Id
                   || id == ControlType.Tree.Id
                   || id == ControlType.TreeItem.Id
                   || id == ControlType.Hyperlink.Id
                   || id == ControlType.Edit.Id
                   || id == ControlType.Group.Id
                   || id == ControlType.Pane.Id
                   || id == ControlType.Custom.Id
                   || id == ControlType.Button.Id
                   || id == ControlType.CheckBox.Id
                   || id == ControlType.ComboBox.Id
                   || id == ControlType.TabItem.Id
                   || id == ControlType.HeaderItem.Id
                   || id == ControlType.MenuItem.Id;
        }

        private static bool IsBroadContainer(ControlType controlType)
        {
            int id = controlType.Id;
            return id == ControlType.Pane.Id
                   || id == ControlType.Group.Id
                   || id == ControlType.Custom.Id
                   || id == ControlType.Document.Id
                   || id == ControlType.Window.Id;
        }

        private static bool LooksLikeContentBlock(System.Windows.Rect bounds, double windowArea, ControlType ct)
        {
            double area = bounds.Width * bounds.Height;
            if (windowArea <= 0) return false;
            double coverage = area / windowArea;

            if (ct.Id == ControlType.Text.Id || ct.Id == ControlType.Document.Id || ct.Id == ControlType.Hyperlink.Id)
                return coverage >= 0.001 && coverage <= MaxWindowCoverage
                       && bounds.Width >= 24 && bounds.Height >= 10;

            return coverage >= 0.008 && coverage <= MaxWindowCoverage
                   && bounds.Width >= 48 && bounds.Height >= 24;
        }

        private static bool RectangleContains(Rectangle outer, Rectangle inner)
        {
            return outer.X <= inner.X && outer.Y <= inner.Y
                   && outer.Right >= inner.Right && outer.Bottom >= inner.Bottom;
        }

        private static double OverlapRatio(Rectangle a, Rectangle b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.Right, b.Right);
            int y2 = Math.Min(a.Bottom, b.Bottom);
            if (x2 <= x1 || y2 <= y1) return 0;
            double overlap = (x2 - x1) * (y2 - y1);
            double areaB = Area(b);
            return areaB > 0 ? overlap / areaB : 0;
        }

        private static long Area(Rectangle r) => (long)r.Width * r.Height;

        /// <summary>
        /// Cheap fingerprint of visible client pixels — used to detect scroll/content changes
        /// without waiting for accessibility events (Chromium often doesn't fire them).
        /// </summary>
        public static int ComputeContentFingerprint(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd))
                return 0;
            if (!GetClientRect(hwnd, out RECT client) || client.Right < 40 || client.Bottom < 40)
                return 0;
            if (!GetWindowRect(hwnd, out RECT windowRect))
                return 0;

            var topLeft = new POINT { X = 0, Y = 0 };
            if (!ClientToScreen(hwnd, ref topLeft))
                return 0;

            int winW = windowRect.Right - windowRect.Left;
            int winH = windowRect.Bottom - windowRect.Top;
            if (winW < 40 || winH < 40) return 0;

            // Sample a narrow vertical strip from the middle of the client area.
            int sampleW = Math.Min(48, Math.Max(16, client.Right / 20));
            int sampleH = Math.Min(client.Bottom - 16, Math.Max(80, client.Bottom * 2 / 3));
            int sampleX = Math.Max(0, (client.Right - sampleW) / 2);
            int sampleY = Math.Max(8, (client.Bottom - sampleH) / 5);
            int offsetX = topLeft.X - windowRect.Left + sampleX;
            int offsetY = topLeft.Y - windowRect.Top + sampleY;

            Bitmap? full = null;
            try
            {
                full = new Bitmap(winW, winH, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(full))
                {
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        if (!PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT) && !PrintWindow(hwnd, hdc, 0))
                            return 0;
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc);
                    }
                }

                if (offsetX + sampleW > full.Width || offsetY + sampleH > full.Height)
                    return 0;

                unchecked
                {
                    int hash = 17;
                    int stepY = Math.Max(1, sampleH / 24);
                    int stepX = Math.Max(1, sampleW / 8);
                    for (int y = offsetY; y < offsetY + sampleH; y += stepY)
                    {
                        for (int x = offsetX; x < offsetX + sampleW; x += stepX)
                        {
                            var c = full.GetPixel(x, y);
                            hash = hash * 31 + c.R;
                            hash = hash * 31 + c.G;
                            hash = hash * 31 + c.B;
                        }
                    }
                    // Mix in size so resize also invalidates
                    hash = hash * 31 + client.Right;
                    hash = hash * 31 + client.Bottom;
                    return hash == 0 ? 1 : hash;
                }
            }
            catch
            {
                return 0;
            }
            finally
            {
                full?.Dispose();
            }
        }
    }
}
