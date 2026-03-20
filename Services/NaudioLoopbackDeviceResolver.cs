using System;
using System.Globalization;
using System.Text;
using NAudio.CoreAudioApi;

namespace SharpShot.Services
{
    /// <summary>
    /// Resolves settings UI strings to a render (playback) <see cref="MMDevice"/> for WASAPI loopback.
    /// Uses the same Windows device model NAudio captures from — no dependency on FFmpeg's WASAPI list.
    /// </summary>
    internal static class NaudioLoopbackDeviceResolver
    {
        public static MMDevice? TryResolve(string? selectedLabel, out string? failureReason)
        {
            failureReason = null;
            var raw = selectedLabel?.Trim() ?? "";
            if (string.IsNullOrEmpty(raw) || string.Equals(raw, "No system audio", StringComparison.OrdinalIgnoreCase))
                return null;

            using var en = new MMDeviceEnumerator();

            if (string.Equals(raw, "Auto-detect", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                }
                catch (Exception ex)
                {
                    failureReason = ex.Message;
                    return null;
                }
            }

            MMDevice? contains = null;
            var normTarget = Normalize(raw);

            foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var name = d.FriendlyName;
                if (string.Equals(name, raw, StringComparison.OrdinalIgnoreCase))
                    return d;

                if (contains == null && name.IndexOf(raw, StringComparison.OrdinalIgnoreCase) >= 0)
                    contains = d;
            }

            if (contains != null)
                return contains;

            if (!string.IsNullOrEmpty(normTarget))
            {
                foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    var nn = Normalize(d.FriendlyName);
                    if (nn == normTarget || (!string.IsNullOrEmpty(nn) && nn.Contains(normTarget, StringComparison.OrdinalIgnoreCase)))
                        return d;
                }
            }

            failureReason = $"No active playback device matched '{raw}'.";
            try
            {
                return en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch
            {
                return null;
            }
        }

        private static string Normalize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s.Normalize(NormalizationForm.FormKD))
            {
                if (char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(c) || c is ' ' or '-' or '_'))
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString().Replace(" ", "", StringComparison.Ordinal);
        }
    }
}
