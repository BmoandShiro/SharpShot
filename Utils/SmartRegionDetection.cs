using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Automation;
using System.Runtime.InteropServices;

namespace SharpShot.Utils
{
    /// <summary>
    /// Uses UI Automation to detect visible elements (images, text, documents, groups)
    /// under a given window and returns their screen bounds for "smart" region highlighting.
    /// </summary>
    public static class SmartRegionDetection
    {
        private const int MinWidth = 24;
        private const int MinHeight = 24;
        private const int MaxRects = 40;

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        public static List<Rectangle> GetDetectedRegions(IntPtr windowHandle)
        {
            var result = new List<Rectangle>();
            if (windowHandle == IntPtr.Zero) return result;

            try
            {
                if (!IsWindowVisible(windowHandle))
                    return result;

                var element = AutomationElement.FromHandle(windowHandle);
                if (element == null) return result;

                var rects = new List<Rectangle>();
                CollectElementRects(element, rects);

                // Sort by area descending so we prefer larger content blocks
                rects.Sort((a, b) => (b.Width * b.Height).CompareTo(a.Width * a.Height));

                // Dedupe: keep larger rects, skip ones mostly contained in another
                foreach (var r in rects)
                {
                    if (result.Count >= MaxRects) break;
                    bool contained = false;
                    foreach (var existing in result)
                    {
                        if (RectangleContains(existing, r) || OverlapRatio(existing, r) > 0.8)
                        {
                            contained = true;
                            break;
                        }
                    }
                    if (!contained)
                        result.Add(r);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SmartRegionDetection: {ex.Message}");
            }

            return result;
        }

        private static void CollectElementRects(AutomationElement element, List<Rectangle> rects)
        {
            try
            {
                if (!element.Current.IsOffscreen)
                {
                    var bounds = element.Current.BoundingRectangle;
                    if (!bounds.IsEmpty && bounds.Width >= MinWidth && bounds.Height >= MinHeight)
                    {
                        var ct = element.Current.ControlType;
                        if (IsInterestingControlType(ct))
                        {
                            var r = new Rectangle(
                                (int)Math.Round(bounds.X),
                                (int)Math.Round(bounds.Y),
                                (int)Math.Round(bounds.Width),
                                (int)Math.Round(bounds.Height));
                            rects.Add(r);
                        }
                    }
                }

                foreach (AutomationElement child in element.FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition))
                {
                    if (rects.Count >= MaxRects * 2) break;
                    CollectElementRects(child, rects);
                }
            }
            catch { /* ignore per-element errors */ }
        }

        private static bool IsInterestingControlType(ControlType controlType)
        {
            if (controlType == null) return false;
            return controlType.Id == ControlType.Image.Id
                   || controlType.Id == ControlType.Document.Id
                   || controlType.Id == ControlType.Text.Id
                   || controlType.Id == ControlType.Group.Id
                   || controlType.Id == ControlType.Pane.Id
                   || controlType.Id == ControlType.List.Id
                   || controlType.Id == ControlType.Table.Id;
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
            double areaB = b.Width * b.Height;
            return areaB > 0 ? overlap / areaB : 0;
        }
    }
}
