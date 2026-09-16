using System;
using System.IO;
using Newtonsoft.Json;
using SharpShot.Models;

namespace SharpShot.Services
{
    public class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SharpShot",
            "settings.json");

        public Settings CurrentSettings { get; private set; }

        public SettingsService()
        {
            CurrentSettings = new Settings();
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonConvert.DeserializeObject<Settings>(json);
                    if (settings != null)
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
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with default settings
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
        }

        public void SaveSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(CurrentSettings, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }
} 