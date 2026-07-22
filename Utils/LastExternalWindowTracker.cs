using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SharpShot.Utils
{
    /// <summary>
    /// Tracks the most recently clicked / activated top-level window that is not SharpShot,
    /// so region select / smart regions can target it even when our toolbar has focus.
    /// </summary>
    public static class LastExternalWindowTracker
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint GA_ROOT = 2;

        private static IntPtr _lastHwnd = IntPtr.Zero;
        private static IntPtr _mouseHook = IntPtr.Zero;
        private static IntPtr _winEventHook = IntPtr.Zero;
        private static LowLevelMouseProc? _mouseProc;
        private static WinEventDelegate? _winEventProc;
        private static uint _ourPid;
        private static bool _started;

        public static void Start()
        {
            if (_started) return;
            _started = true;
            _ourPid = (uint)Process.GetCurrentProcess().Id;

            // Seed from current foreground if it's an external app
            Remember(GetForegroundWindow());

            _mouseProc = MouseHookProc;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);

            _winEventProc = WinEventProc;
            _winEventHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);

            Debug.WriteLine($"LastExternalWindowTracker started (last={_lastHwnd})");
        }

        public static void Stop()
        {
            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
            if (_winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }
            _mouseProc = null;
            _winEventProc = null;
            _started = false;
        }

        /// <summary>
        /// Returns the last non-SharpShot top-level window the user clicked or activated,
        /// or Zero if none / destroyed.
        /// </summary>
        public static IntPtr GetLastWindow()
        {
            if (_lastHwnd == IntPtr.Zero || !IsWindow(_lastHwnd))
            {
                _lastHwnd = IntPtr.Zero;
                return IntPtr.Zero;
            }

            if (!IsExternalVisibleWindow(_lastHwnd))
            {
                _lastHwnd = IntPtr.Zero;
                return IntPtr.Zero;
            }

            return _lastHwnd;
        }

        private static void Remember(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            var root = GetAncestor(hwnd, GA_ROOT);
            if (root == IntPtr.Zero) root = hwnd;

            if (!IsExternalVisibleWindow(root))
                return;

            if (_lastHwnd != root)
            {
                _lastHwnd = root;
                Debug.WriteLine($"LastExternalWindowTracker: {_lastHwnd}");
            }
        }

        private static bool IsExternalVisibleWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !IsWindowVisible(hwnd))
                return false;
            GetWindowThreadProcessId(hwnd, out uint pid);
            return pid != 0 && pid != _ourPid;
        }

        private static IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN)
                {
                    try
                    {
                        var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                        var under = WindowFromPoint(info.pt);
                        Remember(under);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LastExternalWindowTracker mouse: {ex.Message}");
                    }
                }
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == EVENT_SYSTEM_FOREGROUND && hwnd != IntPtr.Zero)
                Remember(hwnd);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

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
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);
    }
}
