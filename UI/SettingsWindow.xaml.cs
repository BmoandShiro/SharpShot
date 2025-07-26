using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SharpShot.Models;
using SharpShot.Services;
using System.Collections.Generic;
using System.Windows.Controls;

namespace SharpShot.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly Settings _originalSettings;

        public SettingsWindow(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _originalSettings = new Settings();
            
            // Copy current settings to avoid modifying the original
            CopySettings(_settingsService.CurrentSettings, _originalSettings);
            
            LoadSettings();
        }

        private void LoadSettings()
        {
            SavePathTextBox.Text = _originalSettings.SavePath;
            
            // Set format combo box
            foreach (var item in FormatComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && 
                    comboItem.Content.ToString() == _originalSettings.ScreenshotFormat)
                {
                    FormatComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Set quality combo box
            foreach (var item in QualityComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && 
                    comboItem.Content.ToString() == _originalSettings.VideoQuality)
                {
                    QualityComboBox.SelectedItem = item;
                    break;
                }
            }
            
            AudioRecordingCheckBox.IsChecked = _originalSettings.EnableAudioRecording;
            GlobalHotkeysCheckBox.IsChecked = _originalSettings.EnableGlobalHotkeys;
            StartMinimizedCheckBox.IsChecked = _originalSettings.StartMinimized;
            
            // Load hotkeys
            RegionHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("RegionCapture", "Double Ctrl");
            FullScreenHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("FullScreenCapture", "Ctrl+Shift+S");
            RecordingHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("ToggleRecording", "Ctrl+Shift+R");
            CancelHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("Cancel", "Escape");
            SaveHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("Save", "Space");
            CopyHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("Copy", "Enter");
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to save screenshots and recordings",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SavePathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            
            var modifiers = new List<string>();
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                modifiers.Add("Ctrl");
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                modifiers.Add("Shift");
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                modifiers.Add("Alt");

            var key = e.Key.ToString();
            if (key == "System")
                return;

            var hotkey = modifiers.Count > 0 ? string.Join("+", modifiers) + "+" + key : key;
            
            if (sender is TextBox textBox)
            {
                textBox.Text = hotkey;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Update settings
                _originalSettings.SavePath = SavePathTextBox.Text;
                
                if (FormatComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem formatItem)
                {
                    _originalSettings.ScreenshotFormat = formatItem.Content.ToString();
                }
                
                if (QualityComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem qualityItem)
                {
                    _originalSettings.VideoQuality = qualityItem.Content.ToString();
                }
                
                _originalSettings.EnableAudioRecording = AudioRecordingCheckBox.IsChecked ?? false;
                _originalSettings.EnableGlobalHotkeys = GlobalHotkeysCheckBox.IsChecked ?? false;
                _originalSettings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
                
                // Update hotkeys
                _originalSettings.Hotkeys["RegionCapture"] = RegionHotkeyTextBox.Text;
                _originalSettings.Hotkeys["FullScreenCapture"] = FullScreenHotkeyTextBox.Text;
                _originalSettings.Hotkeys["ToggleRecording"] = RecordingHotkeyTextBox.Text;
                _originalSettings.Hotkeys["Cancel"] = CancelHotkeyTextBox.Text;
                _originalSettings.Hotkeys["Save"] = SaveHotkeyTextBox.Text;
                _originalSettings.Hotkeys["Copy"] = CopyHotkeyTextBox.Text;
                
                // Copy settings back to the service
                CopySettings(_originalSettings, _settingsService.CurrentSettings);
                
                // Save settings
                _settingsService.SaveSettings();
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CopySettings(Settings source, Settings target)
        {
            target.SavePath = source.SavePath;
            target.ScreenshotFormat = source.ScreenshotFormat;
            target.VideoQuality = source.VideoQuality;
            target.EnableAudioRecording = source.EnableAudioRecording;
            target.EnableGlobalHotkeys = source.EnableGlobalHotkeys;
            target.StartMinimized = source.StartMinimized;
            
            // Copy hotkeys
            target.Hotkeys.Clear();
            foreach (var kvp in source.Hotkeys)
            {
                target.Hotkeys[kvp.Key] = kvp.Value;
            }
        }
    }
} 