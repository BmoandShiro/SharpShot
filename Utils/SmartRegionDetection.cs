using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace SharpShot.Utils
{
    /// <summary>
    /// Uses UI Automation to detect visible content elements under a window
    /// and return their screen bounds for smart region highlighting.
    /// Prefers leaf content (images, text, lists) over giant panes/groups.
    /// </summary>
    public static class SmartRegionDetection
    {
        private const int MinWidth = 20;
        private const int MinHeight = 20;
        private const int MaxRects = 50;
        private const int MaxWalkNodes = 300;
        private const int MaxDepth = 8;
        private const double MaxWindowCoverage = 0.65; // drop rects covering most of the window
        private const uint GA_ROOT = 2;

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
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

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

            // Primary: last window the user clicked/activated that isn't SharpShot
            var last = LastExternalWindowTracker.GetLastWindow();
            if (last != IntPtr.Zero && IsExternalVisibleWindow(last, ourPid))
                return last;

            // Cursor: matches the monitor/content the user is aiming at
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

            // Toolbar click often leaves SharpShot as FG with the cursor over it — fall back
            // to the topmost external window containing the cursor (works across monitors).
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

                var windowBounds = element.Current.BoundingRectangle;
                double windowArea = windowBounds.IsEmpty
                    ? 0
                    : Math.Max(1, windowBounds.Width * windowBounds.Height);

                var rects = new List<Rectangle>();
                int visited = 0;
                CollectElementRects(element, rects, windowArea, depth: 0, ref visited);

                // Prefer smaller content blocks for click/hover targeting
                rects.Sort((a, b) => (a.Width * a.Height).CompareTo(b.Width * b.Height));

                foreach (var r in rects)
                {
                    if (result.Count >= MaxRects) break;

                    bool redundant = false;
                    foreach (var existing in result)
                    {
                        // Skip near-duplicates of a kept (usually smaller) rect
                        if (OverlapRatio(r, existing) > 0.85 && Area(r) <= Area(existing) * 1.2)
                        {
                            redundant = true;
                            break;
                        }
                        // Skip if this larger rect mostly contains an already-kept smaller one
                        // and isn't meaningfully more useful (avoid re-adding parent panes)
                        if (RectangleContains(r, existing) && Area(r) > Area(existing) * 1.5)
                        {
                            redundant = true;
                            break;
                        }
                    }
                    if (!redundant)
                        result.Add(r);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SmartRegionDetection: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Returns the smallest detected region containing the screen point, if any.
        /// </summary>
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
            ref int visited)
        {
            if (visited >= MaxWalkNodes || rects.Count >= MaxRects * 2)
                return;

            try
            {
                visited++;

                if (!element.Current.IsOffscreen)
                {
                    var bounds = element.Current.BoundingRectangle;
                    if (!bounds.IsEmpty && bounds.Width >= MinWidth && bounds.Height >= MinHeight)
                    {
                        var ct = element.Current.ControlType;
                        if (IsInterestingControlType(ct))
                        {
                            double area = bounds.Width * bounds.Height;
                            // Reject giant containers that cover most of the window
                            if (windowArea <= 0 || area / windowArea <= MaxWindowCoverage)
                            {
                                // Pane/Group only if they look like content cards, not chrome
                                if (!IsBroadContainer(ct) || LooksLikeContentCard(bounds, windowArea))
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
                    }
                }

                if (depth >= MaxDepth)
                    return;

                // TreeWalker is much cheaper than FindAll(Children) on large trees (browsers, games UI).
                var walker = TreeWalker.ControlViewWalker;
                var child = walker.GetFirstChild(element);
                while (child != null)
                {
                    if (visited >= MaxWalkNodes || rects.Count >= MaxRects * 2)
                        break;
                    CollectElementRects(child, rects, windowArea, depth + 1, ref visited);
                    child = walker.GetNextSibling(child);
                }
            }
            catch
            {
                // Ignore per-element errors (stale UIA nodes, etc.)
            }
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
                   || id == ControlType.Tree.Id
                   || id == ControlType.TreeItem.Id
                   || id == ControlType.Hyperlink.Id
                   || id == ControlType.Edit.Id
                   || id == ControlType.Group.Id
                   || id == ControlType.Pane.Id
                   || id == ControlType.Custom.Id;
        }

        private static bool IsBroadContainer(ControlType controlType)
        {
            int id = controlType.Id;
            return id == ControlType.Pane.Id
                   || id == ControlType.Group.Id
                   || id == ControlType.Custom.Id
                   || id == ControlType.Document.Id;
        }

        /// <summary>
        /// Allow broad containers only when they look like a mid-size content card,
        /// not the whole browser viewport or tiny chrome chips.
        /// </summary>
        private static bool LooksLikeContentCard(System.Windows.Rect bounds, double windowArea)
        {
            double area = bounds.Width * bounds.Height;
            if (windowArea <= 0) return false;
            double coverage = area / windowArea;
            // Mid-size blocks only (e.g. a card or article column)
            return coverage >= 0.02 && coverage <= MaxWindowCoverage
                   && bounds.Width >= 80 && bounds.Height >= 60;
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
    }
}
