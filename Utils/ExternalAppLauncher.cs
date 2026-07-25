using System;
using System.Diagnostics;
using System.IO;

namespace SharpShot.Utils
{
    public static class ExternalAppLauncher
    {
        public static bool IsValidExecutable(string? path) =>
            !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        public static bool TryLaunch(string? executablePath, out string? error)
        {
            error = null;
            if (!IsValidExecutable(executablePath))
            {
                error = "The linked application path is missing or invalid.";
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty
                };

                // OBS-specific quiet flags when launching OBS
                var fileName = Path.GetFileName(executablePath!);
                if (fileName.Equals("obs64.exe", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("obs32.exe", StringComparison.OrdinalIgnoreCase))
                {
                    startInfo.Arguments = "--disable-updater --disable-crash-handler";
                }

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
