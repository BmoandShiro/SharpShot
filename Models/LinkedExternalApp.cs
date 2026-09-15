using System;
using System.Linq;

namespace SharpShot.Models
{
    public class LinkedExternalApp
    {
        public const int MaxCount = 8;
        public const int MaxLetters = 3;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ExecutablePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Main, Recording, or Both.</summary>
        public string Menu { get; set; } = "Main";

        /// <summary>Window, Camera, Video, Region, Settings, OBS, FullScreen, Star, Play, or Letters.</summary>
        public string Icon { get; set; } = "Window";

        /// <summary>Up to 3 letters shown when Icon is Letters.</summary>
        public string Letters { get; set; } = string.Empty;

        public bool ShowsOnMain =>
            string.Equals(Menu, "Main", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Menu, "Both", StringComparison.OrdinalIgnoreCase);

        public bool ShowsOnRecording =>
            string.Equals(Menu, "Recording", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Menu, "Both", StringComparison.OrdinalIgnoreCase);

        public string NormalizedLetters()
        {
            if (string.IsNullOrWhiteSpace(Letters))
                return FallbackLetters();

            var trimmed = Letters.Trim().ToUpperInvariant();
            if (trimmed.Length > MaxLetters)
                trimmed = trimmed[..MaxLetters];
            return trimmed;
        }

        public string FallbackLetters()
        {
            var source = string.IsNullOrWhiteSpace(DisplayName)
                ? System.IO.Path.GetFileNameWithoutExtension(ExecutablePath)
                : DisplayName;
            if (string.IsNullOrWhiteSpace(source))
                return "APP";
            var compact = new string(source.Where(char.IsLetterOrDigit).ToArray());
            if (compact.Length == 0)
                compact = source.Replace(" ", "");
            if (compact.Length == 0)
                return "APP";
            return compact.Length <= MaxLetters
                ? compact.ToUpperInvariant()
                : compact[..MaxLetters].ToUpperInvariant();
        }

        public LinkedExternalApp Clone() => new()
        {
            Id = Id,
            ExecutablePath = ExecutablePath,
            DisplayName = DisplayName,
            Menu = Menu,
            Icon = Icon,
            Letters = Letters
        };
    }
}
