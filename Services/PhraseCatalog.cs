using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace SharpShot.Services
{
    /// <summary>
    /// English-keyed UI phrases and in-app privacy text. Missing entries stay English.
    /// </summary>
    internal static class PhraseCatalog
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Phrases = LoadPhrases();
        private static readonly Dictionary<string, string> Privacy = LoadPrivacy();

        public static string Translate(string english)
        {
            if (string.IsNullOrEmpty(english) || LocalizationService.CurrentAppLanguage == "en")
                return english;
            if (Phrases.TryGetValue(LocalizationService.CurrentAppLanguage, out var table)
                && table.TryGetValue(english, out var translated)
                && !string.IsNullOrEmpty(translated))
                return translated;
            return english;
        }

        public static bool Contains(string english)
        {
            if (string.IsNullOrEmpty(english))
                return false;
            foreach (var table in Phrases.Values)
            {
                if (table.ContainsKey(english))
                    return true;
            }
            return false;
        }

        public static string PrivacyText()
        {
            if (Privacy.TryGetValue(LocalizationService.CurrentAppLanguage, out var text)
                && !string.IsNullOrWhiteSpace(text))
                return text;
            return Privacy.TryGetValue("en", out var english) ? english : string.Empty;
        }

        private static Dictionary<string, Dictionary<string, string>> LoadPhrases()
        {
            try
            {
                var json = ReadEmbedded("SharpShot.Localization.ui-phrases.json");
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
                return parsed ?? new Dictionary<string, Dictionary<string, string>>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Phrase catalog failed: {ex.Message}");
                return new Dictionary<string, Dictionary<string, string>>();
            }
        }

        private static Dictionary<string, string> LoadPrivacy()
        {
            try
            {
                var json = ReadEmbedded("SharpShot.Localization.privacy.json");
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                return parsed ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Privacy catalog failed: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        private static string ReadEmbedded(string name)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                ?? throw new FileNotFoundException(name);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
