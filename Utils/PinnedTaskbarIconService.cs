using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace SharpShot.Utils
{
    /// <summary>
    /// Keeps pinned / Start Menu shortcuts on a themed .ico so the taskbar pin
    /// follows IconColor. WM_SETICON alone only affects the unpinned running button.
    /// </summary>
    public static class PinnedTaskbarIconService
    {
        public const string AppUserModelId = "BMO.SharpShot";

        private static readonly object SyncLock = new();
        private static string? _lastIconPath;
        private static int _iconGeneration;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNE_UPDATEITEM = 0x00002000;
        private const uint SHCNF_IDLIST = 0x0000;
        private const uint SHCNF_PATHW = 0x0005;
        private const uint SHCNF_FLUSH = 0x1000;

        /// <summary>
        /// Must run before any window is created so the shell groups this process with our shortcuts.
        /// </summary>
        public static void SetProcessAppUserModelId()
        {
            try
            {
                SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set AppUserModelID: {ex.Message}");
            }
        }

        /// <summary>
        /// Writes a themed .ico under %AppData%\SharpShot and points Start Menu /
        /// User Pinned TaskBar shortcuts at it (with matching AUMID).
        /// </summary>
        public static void SyncThemedPinnedIcon(string? colorHex)
        {
            lock (SyncLock)
            {
                try
                {
                    var exePath = Environment.ProcessPath;
                    if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                        return;

                    var appData = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "SharpShot");
                    Directory.CreateDirectory(appData);

                    // Alternate filenames so Windows icon cache notices the change.
                    _iconGeneration ^= 1;
                    var iconPath = Path.Combine(appData, _iconGeneration == 0 ? "taskbar.ico" : "taskbar_alt.ico");
                    ThemedIconHelper.SaveThemedIcoFile(iconPath, colorHex);

                    EnsureStartMenuShortcut(exePath, iconPath);
                    UpdateExistingSharpShotShortcuts(exePath, iconPath);

                    if (!string.IsNullOrEmpty(_lastIconPath)
                        && !string.Equals(_lastIconPath, iconPath, StringComparison.OrdinalIgnoreCase)
                        && File.Exists(_lastIconPath))
                    {
                        try { File.Delete(_lastIconPath); } catch { /* ignore */ }
                    }

                    _lastIconPath = iconPath;

                    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to sync pinned taskbar icon: {ex.Message}");
                }
            }
        }

        private static void EnsureStartMenuShortcut(string exePath, string iconPath)
        {
            // Don't retarget the Start Menu entry at a local bin\Debug|Release build.
            if (IsLocalBuildOutput(exePath))
                return;

            var programs = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs");
            Directory.CreateDirectory(programs);
            var shortcutPath = Path.Combine(programs, "SharpShot.lnk");
            WriteOrUpdateShortcut(shortcutPath, exePath, iconPath, createIfMissing: true);
        }

        private static bool IsLocalBuildOutput(string exePath)
        {
            var normalized = exePath.Replace('/', '\\');
            return normalized.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(@"\bin\x64\Debug\", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(@"\bin\x64\Release\", StringComparison.OrdinalIgnoreCase);
        }

        private static void UpdateExistingSharpShotShortcuts(string exePath, string iconPath)
        {
            var dirs = new List<string>
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    "Programs")
            };

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir))
                    continue;

                IEnumerable<string> lnks;
                try
                {
                    lnks = Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories);
                }
                catch
                {
                    continue;
                }

                foreach (var lnk in lnks)
                {
                    try
                    {
                        if (!IsSharpShotShortcut(lnk, exePath))
                            continue;

                        // Don't recreate Start Menu entry twice with createIfMissing.
                        WriteOrUpdateShortcut(lnk, targetExeOverride: null, iconPath, createIfMissing: false);
                    }
                    catch
                    {
                        // Skip unreadable shortcuts
                    }
                }
            }
        }

        private static bool IsSharpShotShortcut(string shortcutPath, string currentExePath)
        {
            var name = Path.GetFileNameWithoutExtension(shortcutPath);
            if (name.Equals("SharpShot", StringComparison.OrdinalIgnoreCase))
                return true;

            var target = ResolveShortcutTarget(shortcutPath);
            if (string.IsNullOrWhiteSpace(target))
                return false;

            if (string.Equals(target, currentExePath, StringComparison.OrdinalIgnoreCase))
                return true;

            return Path.GetFileName(target).Equals("SharpShot.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteOrUpdateShortcut(
            string shortcutPath,
            string? targetExeOverride,
            string iconPath,
            bool createIfMissing)
        {
            IShellLinkW? link = null;
            IPersistFile? persist = null;
            IPropertyStore? store = null;
            IntPtr aumidMem = IntPtr.Zero;

            try
            {
                link = (IShellLinkW)new ShellLink();
                persist = (IPersistFile)link;

                var exists = File.Exists(shortcutPath);
                if (exists)
                {
                    persist.Load(shortcutPath, 0);
                }
                else if (!createIfMissing)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(targetExeOverride))
                {
                    link.SetPath(targetExeOverride);
                    var workDir = Path.GetDirectoryName(targetExeOverride);
                    if (!string.IsNullOrEmpty(workDir))
                        link.SetWorkingDirectory(workDir);
                    link.SetDescription("SharpShot");
                }

                link.SetIconLocation(iconPath, 0);

                try
                {
                    store = (IPropertyStore)link;
                    var key = new PropertyKey
                    {
                        Fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
                        Pid = 5 // PKEY_AppUserModel_ID
                    };
                    aumidMem = Marshal.StringToCoTaskMemUni(AppUserModelId);
                    var pv = new PropVariant
                    {
                        vt = 31, // VT_LPWSTR
                        pointerValue = aumidMem
                    };
                    store.SetValue(ref key, ref pv);
                    store.Commit();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to set shortcut AUMID: {ex.Message}");
                }

                persist.Save(shortcutPath, true);

                var pathPtr = Marshal.StringToHGlobalUni(shortcutPath);
                try
                {
                    SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW | SHCNF_FLUSH, pathPtr, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeHGlobal(pathPtr);
                }
            }
            finally
            {
                if (aumidMem != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(aumidMem);
                if (store != null)
                    Marshal.ReleaseComObject(store);
                if (persist != null)
                    Marshal.ReleaseComObject(persist);
                if (link != null)
                    Marshal.ReleaseComObject(link);
            }
        }

        private static string ResolveShortcutTarget(string shortcutPath)
        {
            IShellLinkW? link = null;
            IPersistFile? persist = null;
            try
            {
                link = (IShellLinkW)new ShellLink();
                persist = (IPersistFile)link;
                persist.Load(shortcutPath, 0);

                var sb = new StringBuilder(260);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                return sb.ToString();
            }
            finally
            {
                if (persist != null)
                    Marshal.ReleaseComObject(persist);
                if (link != null)
                    Marshal.ReleaseComObject(link);
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        private interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey key, out PropVariant pv);
            void SetValue(ref PropertyKey key, ref PropVariant pv);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PropertyKey
        {
            public Guid Fmtid;
            public uint Pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PropVariant
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pointerValue;
        }
    }
}
