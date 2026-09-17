using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SharpShot.Utils
{
    /// <summary>
    /// Keeps SharpShot HWNDs out of desktop captures (DXGI and BitBlt) without changing visibility,
    /// so menus and focus are not dismissed.
    /// </summary>
    internal static class CaptureExclusion
    {
        private const uint WdaNone = 0;
        private const uint WdaExcludeFromCapture = 0x00000011;

        private static bool _supported = true;

        public static bool IsSupported => _supported;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        private const int DwmwaCloak = 13;

        public static void SyncOpenWindows(bool exclude)
        {
            var app = Application.Current;
            if (app?.Dispatcher == null)
                return;

            void Apply()
            {
                foreach (Window window in app.Windows)
                {
                    if (!IsSharpShotWindow(window))
                        continue;
                    TrySet(window, exclude);
                }
            }

            if (app.Dispatcher.CheckAccess())
                Apply();
            else
                app.Dispatcher.Invoke(Apply);
        }

        public static void TrySet(Window? window, bool exclude)
        {
            if (window == null || !_supported)
                return;

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            if (SetWindowDisplayAffinity(hwnd, exclude ? WdaExcludeFromCapture : WdaNone))
                return;

            // 0x11 is unsupported before Windows 10 2004. Fall back to visibility hiding.
            _supported = false;
        }

        public static bool TrySetCloaked(Window? window, bool cloaked)
        {
            if (window == null)
                return false;

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            int value = cloaked ? 1 : 0;
            return DwmSetWindowAttribute(hwnd, DwmwaCloak, ref value, sizeof(int)) == 0;
        }

        public static bool IsSharpShotWindow(Window window)
        {
            var ns = window.GetType().Namespace ?? "";
            return ns == "SharpShot" || ns == "SharpShot.UI";
        }
    }
}
