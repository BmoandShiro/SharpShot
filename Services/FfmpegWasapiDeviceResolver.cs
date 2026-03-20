using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SharpShot.Services
{
    /// <summary>
    /// Maps user-facing playback device names (NAudio/WMI/Core Audio) to strings FFmpeg's WASAPI demuxer
    /// accepts for <c>-i</c>. Those sources rarely match byte-for-byte; we always cross-check FFmpeg's own list.
    /// </summary>
    internal static class FfmpegWasapiDeviceResolver
    {
        private static readonly object Sync = new();
        private static List<string>? _cachedNames;
        private static string? _cachedExe;
        private static DateTime _cachedUtc;

        private const int CacheSeconds = 45;

        /// <summary>Returns quoted device names from FFmpeg WASAPI enumeration (stderr).</summary>
        public static IReadOnlyList<string> GetWasapiAudioDeviceNames(string ffmpegPath)
        {
            lock (Sync)
            {
                if (!string.IsNullOrEmpty(ffmpegPath)
                    && _cachedNames != null
                    && ffmpegPath.Equals(_cachedExe, StringComparison.OrdinalIgnoreCase)
                    && (DateTime.UtcNow - _cachedUtc).TotalSeconds < CacheSeconds)
                {
                    return _cachedNames;
                }
            }

            var list = QueryFfmpegWasapiList(ffmpegPath);

            lock (Sync)
            {
                _cachedNames = list;
                _cachedExe = ffmpegPath;
                _cachedUtc = DateTime.UtcNow;
                return list;
            }
        }

        private static List<string> QueryFfmpegWasapiList(string ffmpegPath)
        {
            var devices = new List<string>();
            try
            {
                if (string.IsNullOrWhiteSpace(ffmpegPath))
                    return devices;

                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-hide_banner -list_devices true -f wasapi -i dummy",
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                if (!p.Start())
                    return devices;

                var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit(15000);

                var lines = stderr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var inWasapiAudio = false;
                var linesSinceHeader = 0;

                foreach (var line in lines)
                {
                    var t = line.Trim();

                    if (t.Contains("WASAPI audio devices", StringComparison.OrdinalIgnoreCase))
                    {
                        inWasapiAudio = true;
                        linesSinceHeader = 0;
                        continue;
                    }

                    if (!inWasapiAudio)
                        continue;

                    linesSinceHeader++;

                    // Stop if FFmpeg moves on to another major block (avoid unrelated quoted strings)
                    if (linesSinceHeader > 2 && (t.StartsWith("Input #", StringComparison.Ordinal) ||
                                                 t.StartsWith("Output #", StringComparison.Ordinal)))
                    {
                        break;
                    }

                    if (linesSinceHeader > 60)
                        break;

                    if (!t.Contains('"', StringComparison.Ordinal))
                        continue;

                    var start = t.IndexOf('"');
                    var end = t.LastIndexOf('"');
                    if (start < 0 || end <= start)
                        continue;

                    var name = t.Substring(start + 1, end - start - 1);
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    // Loopback uses render (playback) endpoints; skip obvious capture-only lines when labeled
                    var lower = t.ToLowerInvariant();
                    if (lower.Contains("capture") && !lower.Contains("playback") && !lower.Contains("loopback"))
                        continue;

                    if (!devices.Contains(name))
                        devices.Add(name);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FfmpegWasapiDeviceResolver: list failed: {ex.Message}");
            }

            return devices;
        }

        /// <summary>Strip dshow-style prefix; return string to pass to WASAPI -i (after quoting).</summary>
        public static string ResolveLoopbackDeviceForFfmpeg(string ffmpegPath, string userSelected)
        {
            var raw = userSelected.Trim();
            if (raw.StartsWith("audio=", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring("audio=".Length).Trim();

            var names = GetWasapiAudioDeviceNames(ffmpegPath);
            if (names.Count == 0)
                return raw;

            foreach (var n in names)
            {
                if (string.Equals(n, raw, StringComparison.OrdinalIgnoreCase))
                    return n;
            }

            // Friendly name contains canonical or vice versa (e.g. "Speakers" vs "Speakers (Realtek(R) Audio)")
            foreach (var n in names)
            {
                if (raw.Length >= 3 && n.Contains(raw, StringComparison.OrdinalIgnoreCase))
                    return n;
            }

            foreach (var n in names)
            {
                if (n.Length >= 8 && raw.Contains(n, StringComparison.OrdinalIgnoreCase))
                    return n;
            }

            // Last resort: normalized spacing
            var rawNorm = Normalize(raw);
            foreach (var n in names)
            {
                if (Normalize(n) == rawNorm)
                    return n;
            }

            return raw;
        }

        private static string Normalize(string s)
        {
            var parts = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', parts).ToLowerInvariant();
        }
    }
}
