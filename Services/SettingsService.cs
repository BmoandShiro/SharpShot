using System;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using SharpShot.Models;

namespace SharpShot.Services
{
    public class SettingsService
    {
        private const string AppFolderName = "SharpShot";
        private const string SettingsFileName = "settings.json";
        private const string SettingsBackupFileName = "settings.json.bak";
        private const string SettingsTempFileName = "settings.json.tmp";
        private const int ReadAttempts = 4;

        private readonly object _ioLock = new();

        /// <summary>
        /// True after a real settings file loaded, or when this is a first run with no file.
        /// False when every candidate failed to read, so a later save cannot overwrite them with defaults.
        /// </summary>
        private bool _persistAllowed;

        public Settings CurrentSettings { get; private set; }

        public SettingsService()
        {
            CurrentSettings = new Settings();
        }

        public void LoadSettings()
        {
            var roamingDirectory = GetAppDirectory(Environment.SpecialFolder.ApplicationData);
            var localDirectory = GetAppDirectory(Environment.SpecialFolder.LocalApplicationData);

            string? roamingSettings = roamingDirectory == null ? null : Path.Combine(roamingDirectory, SettingsFileName);
            string? roamingBackup = roamingDirectory == null ? null : Path.Combine(roamingDirectory, SettingsBackupFileName);
            string? localSettings = localDirectory == null ? null : Path.Combine(localDirectory, SettingsFileName);

            var candidates = new[] { roamingSettings, roamingBackup, localSettings };
            bool anyFailed = false;

            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null)
                    continue;

                switch (TryLoadCandidate(candidate, isPrimary: i == 0))
                {
                    case ProbeResult.Loaded:
                        _persistAllowed = true;
                        return;
                    case ProbeResult.Failed:
                        anyFailed = true;
                        break;
                }
            }

            // Profile not ready: do not treat a missed roaming folder as a first run.
            _persistAllowed = roamingDirectory != null && !anyFailed;
            if (!_persistAllowed)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Settings were not loaded. Saves are disabled so defaults cannot replace the existing file.");
            }
        }

        public void SaveSettings()
        {
            if (!_persistAllowed)
                return;

            lock (_ioLock)
            {
                string json;
                try
                {
                    json = JsonConvert.SerializeObject(CurrentSettings, Formatting.Indented);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to serialize settings: {ex.Message}");
                    return;
                }

                var roamingDirectory = GetAppDirectory(Environment.SpecialFolder.ApplicationData);
                if (roamingDirectory == null)
                {
                    System.Diagnostics.Debug.WriteLine("Skipping settings save because the roaming profile folder is not available.");
                    return;
                }

                try
                {
                    WriteDurable(roamingDirectory, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                }

                var localDirectory = GetAppDirectory(Environment.SpecialFolder.LocalApplicationData);
                if (localDirectory == null)
                    return;

                try
                {
                    WriteDurable(localDirectory, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to mirror settings: {ex.Message}");
                }
            }
        }

        private ProbeResult TryLoadCandidate(string path, bool isPrimary)
        {
            try
            {
                if (!File.Exists(path))
                    return ProbeResult.Missing;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to check settings file '{path}': {ex.Message}");
                return ProbeResult.Failed;
            }

            for (int attempt = 0; attempt < ReadAttempts; attempt++)
            {
                try
                {
                    var json = File.ReadAllText(path);
                    if (!TryDeserialize(json, out var settings) || settings == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Settings file was empty or invalid: {path}");
                        return ProbeResult.Failed;
                    }

                    ApplyLoadedSettings(settings, json);
                    if (!isPrimary)
                        System.Diagnostics.Debug.WriteLine($"Loaded settings from fallback: {path}");
                    return ProbeResult.Loaded;
                }
                catch (FileNotFoundException)
                {
                    return ProbeResult.Missing;
                }
                catch (DirectoryNotFoundException)
                {
                    return ProbeResult.Missing;
                }
                catch (IOException) when (attempt < ReadAttempts - 1)
                {
                    Thread.Sleep(40 * (attempt + 1));
                }
                catch (UnauthorizedAccessException) when (attempt < ReadAttempts - 1)
                {
                    Thread.Sleep(40 * (attempt + 1));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load settings from '{path}': {ex.Message}");
                    return ProbeResult.Failed;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Settings file stayed locked: {path}");
            return ProbeResult.Failed;
        }

        private void ApplyLoadedSettings(Settings settings, string json)
        {
            CurrentSettings = settings;
            CurrentSettings.MigrateLegacyLinkedApps();
            if (json.IndexOf("UseDenseOcrForSmartRegions", StringComparison.OrdinalIgnoreCase) < 0)
                CurrentSettings.UseDenseOcrForSmartRegions = true;
            // Existing installs: restore OBS independently of the custom launcher,
            // and keep the custom button they already have on the recording toolbar.
            if (json.IndexOf("ShowObsButtonOnRecordingToolbar", StringComparison.OrdinalIgnoreCase) < 0)
                CurrentSettings.ShowObsButtonOnRecordingToolbar = true;
            if (json.IndexOf("ShowCustomAppButtonOnRecordingToolbar", StringComparison.OrdinalIgnoreCase) < 0)
                CurrentSettings.ShowCustomAppButtonOnRecordingToolbar = true;
            if (string.IsNullOrWhiteSpace(CurrentSettings.SelectedInputAudioDevice))
                CurrentSettings.SelectedInputAudioDevice = "Auto-detect";
            if (json.IndexOf("SmartRegionHorizontalSplit", StringComparison.OrdinalIgnoreCase) < 0)
            {
                // The old on/off toggle was too aggressive and split sentences.
                // Off stays off; on maps to "far apart" so normal sentences stay together.
                bool oldSplitOff = json.IndexOf("\"SmartRegionSplitOnLargeGap\": false", StringComparison.OrdinalIgnoreCase) >= 0
                    || json.IndexOf("\"SmartRegionSplitOnLargeGap\":false", StringComparison.OrdinalIgnoreCase) >= 0;
                CurrentSettings.SmartRegionHorizontalSplit = oldSplitOff ? 0 : 1;
            }
        }

        /// <summary>
        /// Writes JSON via a temp file, then replaces the destination.
        /// A corrupt destination is removed instead of being promoted to the .bak,
        /// so a recovered backup is not overwritten by the bad file.
        /// </summary>
        private static void WriteDurable(string directory, string json)
        {
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, SettingsFileName);
            var temp = Path.Combine(directory, SettingsTempFileName);
            var backup = Path.Combine(directory, SettingsBackupFileName);

            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            switch (InspectDestination(destination))
            {
                case DestState.Missing:
                    File.Move(temp, destination);
                    break;
                case DestState.Valid:
                    File.Replace(temp, destination, backup, ignoreMetadataErrors: true);
                    break;
                case DestState.Corrupt:
                    File.Delete(destination);
                    File.Move(temp, destination);
                    break;
                default:
                    TryDelete(temp);
                    throw new IOException($"Settings file is locked: {destination}");
            }
        }

        private static DestState InspectDestination(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return DestState.Missing;
            }
            catch (IOException)
            {
                return DestState.Unreadable;
            }
            catch (UnauthorizedAccessException)
            {
                return DestState.Unreadable;
            }

            try
            {
                var json = File.ReadAllText(path);
                return TryDeserialize(json, out _) ? DestState.Valid : DestState.Corrupt;
            }
            catch (JsonException)
            {
                return DestState.Corrupt;
            }
            catch (IOException)
            {
                return DestState.Unreadable;
            }
            catch (UnauthorizedAccessException)
            {
                return DestState.Unreadable;
            }
        }

        private static bool TryDeserialize(string? json, out Settings? settings)
        {
            settings = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            settings = JsonConvert.DeserializeObject<Settings>(json);
            return settings != null;
        }

        /// <summary>
        /// Known-folder path for this process. Empty when the profile is not ready yet,
        /// which must not become a relative SharpShot folder.
        /// </summary>
        private static string? GetAppDirectory(Environment.SpecialFolder folder)
        {
            var root = Environment.GetFolderPath(folder);
            if (string.IsNullOrWhiteSpace(root))
                return null;

            return Path.Combine(root, AppFolderName);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not delete '{path}': {ex.Message}");
            }
        }

        private enum ProbeResult
        {
            Missing,
            Loaded,
            Failed
        }

        private enum DestState
        {
            Missing,
            Valid,
            Corrupt,
            Unreadable
        }
    }
}
