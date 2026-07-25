using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace SharpShot.Utils
{
    public sealed class InstalledAppInfo
    {
        public string DisplayName { get; init; } = string.Empty;
        public string ExecutablePath { get; init; } = string.Empty;

        public override string ToString() => DisplayName;
    }

    public static class InstalledAppEnumerator
    {
        public static IReadOnlyList<InstalledAppInfo> GetInstalledApps()
        {
            var results = new Dictionary<string, InstalledAppInfo>(StringComparer.OrdinalIgnoreCase);
            var roots = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs")
            };

            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                IEnumerable<string> lnkFiles;
                try
                {
                    lnkFiles = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories);
                }
                catch
                {
                    continue;
                }

                foreach (var lnk in lnkFiles)
                {
                    try
                    {
                        var target = ResolveShortcutTarget(lnk);
                        if (string.IsNullOrWhiteSpace(target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!File.Exists(target))
                            continue;

                        var displayName = Path.GetFileNameWithoutExtension(lnk);
                        if (string.IsNullOrWhiteSpace(displayName))
                            displayName = Path.GetFileNameWithoutExtension(target);

                        // Prefer first occurrence; skip duplicates by exe path
                        if (!results.ContainsKey(target))
                        {
                            results[target] = new InstalledAppInfo
                            {
                                DisplayName = displayName,
                                ExecutablePath = target
                            };
                        }
                    }
                    catch
                    {
                        // Skip unreadable shortcuts
                    }
                }
            }

            return results.Values
                .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
    }
}
