using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace SharpShot
{
    /// <summary>
    /// Current app version for UI. Independent of the GitHub updater so Steam/Store builds
    /// can hide updates without losing the settings footer.
    /// </summary>
    public static class AppVersion
    {
        public static Version GetCurrentVersion()
        {
            try
            {
                var versionPath = Path.Combine(AppContext.BaseDirectory, "Version");
                if (File.Exists(versionPath))
                {
                    var text = File.ReadAllText(versionPath).Trim().TrimStart('v', 'V');
                    if (Version.TryParse(text, out var fromFile))
                        return fromFile;
                }
            }
            catch
            {
                // fall through
            }

            try
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var fvi = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(fvi.FileVersion) &&
                        Version.TryParse(fvi.FileVersion, out var fromFvi))
                        return fromFvi;
                }
            }
            catch
            {
                // fall through
            }

            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
        }

        public static string GetCurrentVersionDisplay()
        {
            return $"v{GetCurrentVersion()}";
        }
    }
}
