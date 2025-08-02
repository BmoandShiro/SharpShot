using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SharpShot.Models;
using SharpShot.Services;
using SharpShot.Utils;
using ScreenRecorderLib; // Added for ScreenRecorderLib

namespace SharpShot.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly Settings _originalSettings;
        private readonly HotkeyManager? _hotkeyManager;
        private TextBox? _currentHotkeyTextBox;
        private bool _isListeningForHotkey = false;

        public SettingsWindow(SettingsService settingsService, HotkeyManager? hotkeyManager = null)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _hotkeyManager = hotkeyManager;
            _originalSettings = new Settings();
            
            // Copy current settings to avoid modifying the original
            CopySettings(_settingsService.CurrentSettings, _originalSettings);
            
            // Populate screen dropdown with actual monitors
            PopulateScreenDropdown();
            
            LoadSettings();
            
            // Add event handlers for sliders
            HoverOpacitySlider.ValueChanged += (s, e) => UpdateOpacityLabels();
            DropShadowOpacitySlider.ValueChanged += (s, e) => UpdateOpacityLabels();
            
            // Add event handler for magnifier checkbox
            EnableMagnifierCheckBox.Checked += (s, e) => MagnifierZoomPanel.Visibility = Visibility.Visible;
            EnableMagnifierCheckBox.Unchecked += (s, e) => MagnifierZoomPanel.Visibility = Visibility.Collapsed;
            
            // Add event handler for color wheel text box
            IconColorTextBox.TextChanged += IconColorTextBox_TextChanged;
            
            // Set initial visibility
            MagnifierZoomPanel.Visibility = _originalSettings.EnableMagnifier ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging the window by clicking anywhere on it
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void TopResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Let the window handle resize automatically
            e.Handled = false;
        }

        private void BottomResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Let the window handle resize automatically
            e.Handled = false;
        }

        private void LeftResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Let the window handle resize automatically
            e.Handled = false;
        }

        private void RightResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Let the window handle resize automatically
            e.Handled = false;
        }

        private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void LoadSettings()
        {
            try
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
            
            // Set screen combo box
            foreach (var item in ScreenComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && 
                    comboItem.Content.ToString() == _originalSettings.SelectedScreen)
                {
                    ScreenComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Set recording engine combo box
            foreach (var item in RecordingEngineComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && 
                    comboItem.Content.ToString() == _originalSettings.RecordingEngine)
                {
                    RecordingEngineComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Set audio recording mode combo box
            foreach (var item in AudioRecordingModeComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && 
                    comboItem.Content.ToString() == _originalSettings.AudioRecordingMode)
                {
                    AudioRecordingModeComboBox.SelectedItem = item;
                    break;
                }
            }

            // Load audio devices
            LoadAudioDevices();
            
            // Set selected audio devices
            SetSelectedAudioDevices();
            GlobalHotkeysCheckBox.IsChecked = _originalSettings.EnableGlobalHotkeys;
            StartMinimizedCheckBox.IsChecked = _originalSettings.StartMinimized;
            AutoCopyScreenshotsCheckBox.IsChecked = _originalSettings.AutoCopyScreenshots;
            EnableMagnifierCheckBox.IsChecked = _originalSettings.EnableMagnifier;
            
            // Load magnifier zoom level
            if (MagnifierZoomComboBox != null && MagnifierZoomComboBox.Items.Count > 0)
            {
                var zoomText = $"{_originalSettings.MagnifierZoomLevel:F1}x";
                foreach (System.Windows.Controls.ComboBoxItem item in MagnifierZoomComboBox.Items)
                {
                    if (item.Content?.ToString() == zoomText)
                    {
                        MagnifierZoomComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
            
            // Load theme customization settings
            IconColorTextBox.Text = _originalSettings.IconColor;
            IconColorWheel.SetColor(_originalSettings.IconColor);
            IconColorWheel.ColorChanged += (s, color) => IconColorTextBox.Text = color;
            HoverOpacitySlider.Value = _originalSettings.HoverOpacity;
            DropShadowOpacitySlider.Value = _originalSettings.DropShadowOpacity;
            UpdateOpacityLabels();
            
            // Load hotkeys - use blank defaults unless user has set them
            ScreenshotRegionHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("ScreenshotRegion", "");
            ScreenshotFullscreenHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("ScreenshotFullscreen", "");
            RecordRegionHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("RecordRegion", "");
            RecordFullscreenHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("RecordFullscreen", "");
            CopyHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("Copy", "");
            SaveHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("Save", "");
            
            // Load triple-click settings
            ScreenshotRegionTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("ScreenshotRegionTripleClick", "false") == "true";
            ScreenshotFullscreenTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("ScreenshotFullscreenTripleClick", "false") == "true";
            RecordRegionTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("RecordRegionTripleClick", "false") == "true";
            RecordFullscreenTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("RecordFullscreenTripleClick", "false") == "true";
            CopyTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("CopyTripleClick", "false") == "true";
            SaveTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("SaveTripleClick", "false") == "true";
            
            // Apply current theme colors
            UpdateThemeColors();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                // Continue with default values if loading fails
            }
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
            if (!_isListeningForHotkey) return;
            
            e.Handled = true;
            
            var modifiers = new List<string>();
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                modifiers.Add("Ctrl");
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                modifiers.Add("Shift");
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                modifiers.Add("Alt");

            var key = e.Key.ToString();
            
            // Skip system keys
            if (key == "System")
                return;

            // Handle single modifier keys
            if (key == "LeftCtrl" || key == "RightCtrl")
            {
                if (_currentHotkeyTextBox != null)
                {
                    _currentHotkeyTextBox.Text = "Ctrl";
                    _currentHotkeyTextBox.IsReadOnly = true;
                    // Automatically enable triple-click for single modifier keys
                    EnableTripleClickForHotkey(_currentHotkeyTextBox);
                }
                _isListeningForHotkey = false;
                _currentHotkeyTextBox = null;
                return;
            }
            
            if (key == "LeftShift" || key == "RightShift")
            {
                if (_currentHotkeyTextBox != null)
                {
                    _currentHotkeyTextBox.Text = "Shift";
                    _currentHotkeyTextBox.IsReadOnly = true;
                    // Automatically enable triple-click for single modifier keys
                    EnableTripleClickForHotkey(_currentHotkeyTextBox);
                }
                _isListeningForHotkey = false;
                _currentHotkeyTextBox = null;
                return;
            }
            
            if (key == "LeftAlt" || key == "RightAlt")
            {
                if (_currentHotkeyTextBox != null)
                {
                    _currentHotkeyTextBox.Text = "Alt";
                    _currentHotkeyTextBox.IsReadOnly = true;
                    // Automatically enable triple-click for single modifier keys
                    EnableTripleClickForHotkey(_currentHotkeyTextBox);
                }
                _isListeningForHotkey = false;
                _currentHotkeyTextBox = null;
                return;
            }

            var hotkey = modifiers.Count > 0 ? string.Join("+", modifiers) + "+" + key : key;
            
            if (_currentHotkeyTextBox != null)
            {
                _currentHotkeyTextBox.Text = hotkey;
                _currentHotkeyTextBox.IsReadOnly = true;
            }
            
            _isListeningForHotkey = false;
            _currentHotkeyTextBox = null;
        }
        
        private void EnableTripleClickForHotkey(TextBox hotkeyTextBox)
        {
            // Find the corresponding triple-click checkbox based on the hotkey text box name
            if (hotkeyTextBox == ScreenshotRegionHotkeyTextBox)
                ScreenshotRegionTripleClickCheckBox.IsChecked = true;
            else if (hotkeyTextBox == ScreenshotFullscreenHotkeyTextBox)
                ScreenshotFullscreenTripleClickCheckBox.IsChecked = true;
            else if (hotkeyTextBox == RecordRegionHotkeyTextBox)
                RecordRegionTripleClickCheckBox.IsChecked = true;
            else if (hotkeyTextBox == RecordFullscreenHotkeyTextBox)
                RecordFullscreenTripleClickCheckBox.IsChecked = true;
            else if (hotkeyTextBox == CopyHotkeyTextBox)
                CopyTripleClickCheckBox.IsChecked = true;
            else if (hotkeyTextBox == SaveHotkeyTextBox)
                SaveTripleClickCheckBox.IsChecked = true;
        }
        
        private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                _currentHotkeyTextBox = textBox;
                _isListeningForHotkey = true;
                textBox.IsReadOnly = false;
                textBox.Text = "Press a key...";
                textBox.Focus();
            }
        }
        
        private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isListeningForHotkey)
            {
                _isListeningForHotkey = false;
                if (_currentHotkeyTextBox != null)
                {
                    _currentHotkeyTextBox.IsReadOnly = true;
                    // If no key was pressed, clear the text
                    if (_currentHotkeyTextBox.Text == "Press a key...")
                    {
                        _currentHotkeyTextBox.Text = "";
                    }
                }
                _currentHotkeyTextBox = null;
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
                    _originalSettings.ScreenshotFormat = formatItem.Content?.ToString() ?? "PNG";
                }
                
                if (QualityComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem qualityItem)
                {
                    _originalSettings.VideoQuality = qualityItem.Content?.ToString() ?? "High";
                }
                
                if (ScreenComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem screenItem)
                {
                    _originalSettings.SelectedScreen = screenItem.Content?.ToString() ?? "Primary Monitor";
                }
                
                if (RecordingEngineComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem recordingEngineItem)
                {
                    _originalSettings.RecordingEngine = recordingEngineItem.Content?.ToString() ?? "ScreenRecorderLib";
                }
                
                if (AudioRecordingModeComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem audioModeItem)
                {
                    _originalSettings.AudioRecordingMode = audioModeItem.Content?.ToString() ?? "No Audio";
                }

                // Save selected audio devices
                if (OutputAudioDeviceComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem outputAudioItem)
                {
                    _originalSettings.SelectedOutputAudioDevice = outputAudioItem.Content?.ToString() ?? string.Empty;
                }

                if (InputAudioDeviceComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem inputAudioItem)
                {
                    _originalSettings.SelectedInputAudioDevice = inputAudioItem.Content?.ToString() ?? string.Empty;
                }
                _originalSettings.EnableGlobalHotkeys = GlobalHotkeysCheckBox.IsChecked ?? false;
                _originalSettings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
                _originalSettings.AutoCopyScreenshots = AutoCopyScreenshotsCheckBox.IsChecked ?? false;
                _originalSettings.EnableMagnifier = EnableMagnifierCheckBox.IsChecked ?? false;
                
                // Save magnifier zoom level
                if (MagnifierZoomComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem zoomItem)
                {
                    var zoomText = zoomItem.Content?.ToString() ?? "2.0x";
                    if (double.TryParse(zoomText.Replace("x", ""), out double zoomLevel))
                    {
                        _originalSettings.MagnifierZoomLevel = zoomLevel;
                    }
                }
                
                // Save theme customization settings
                _originalSettings.IconColor = IconColorTextBox.Text;
                _originalSettings.HoverOpacity = HoverOpacitySlider.Value;
                _originalSettings.DropShadowOpacity = DropShadowOpacitySlider.Value;
                
                // Update theme colors immediately
                UpdateThemeColors();
                
                // Update hotkeys - only save non-empty values
                if (!string.IsNullOrWhiteSpace(ScreenshotRegionHotkeyTextBox.Text))
                    _originalSettings.Hotkeys["ScreenshotRegion"] = ScreenshotRegionHotkeyTextBox.Text;
                if (!string.IsNullOrWhiteSpace(ScreenshotFullscreenHotkeyTextBox.Text))
                    _originalSettings.Hotkeys["ScreenshotFullscreen"] = ScreenshotFullscreenHotkeyTextBox.Text;
                if (!string.IsNullOrWhiteSpace(RecordRegionHotkeyTextBox.Text))
                    _originalSettings.Hotkeys["RecordRegion"] = RecordRegionHotkeyTextBox.Text;
                if (!string.IsNullOrWhiteSpace(RecordFullscreenHotkeyTextBox.Text))
                    _originalSettings.Hotkeys["RecordFullscreen"] = RecordFullscreenHotkeyTextBox.Text;
                if (!string.IsNullOrWhiteSpace(CopyHotkeyTextBox.Text))
                    _originalSettings.Hotkeys["Copy"] = CopyHotkeyTextBox.Text;
                if (!string.IsNullOrWhiteSpace(SaveHotkeyTextBox.Text))
                    _originalSettings.Hotkeys["Save"] = SaveHotkeyTextBox.Text;
                
                // Update triple-click settings
                _originalSettings.Hotkeys["ScreenshotRegionTripleClick"] = ScreenshotRegionTripleClickCheckBox.IsChecked == true ? "true" : "false";
                _originalSettings.Hotkeys["ScreenshotFullscreenTripleClick"] = ScreenshotFullscreenTripleClickCheckBox.IsChecked == true ? "true" : "false";
                _originalSettings.Hotkeys["RecordRegionTripleClick"] = RecordRegionTripleClickCheckBox.IsChecked == true ? "true" : "false";
                _originalSettings.Hotkeys["RecordFullscreenTripleClick"] = RecordFullscreenTripleClickCheckBox.IsChecked == true ? "true" : "false";
                _originalSettings.Hotkeys["CopyTripleClick"] = CopyTripleClickCheckBox.IsChecked == true ? "true" : "false";
                _originalSettings.Hotkeys["SaveTripleClick"] = SaveTripleClickCheckBox.IsChecked == true ? "true" : "false";
                
                // Copy settings back to the service
                CopySettings(_originalSettings, _settingsService.CurrentSettings);
                
                // Save settings
                _settingsService.SaveSettings();
                
                // Apply theme changes immediately
                ApplyThemeChanges();
                
                // Update hotkeys if hotkey manager is available
                _hotkeyManager?.UpdateHotkeys();
                
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
            target.RecordingEngine = source.RecordingEngine;
            target.AudioRecordingMode = source.AudioRecordingMode;
            target.SelectedOutputAudioDevice = source.SelectedOutputAudioDevice;
            target.SelectedInputAudioDevice = source.SelectedInputAudioDevice;
            target.EnableGlobalHotkeys = source.EnableGlobalHotkeys;
            target.StartMinimized = source.StartMinimized;
            target.IconColor = source.IconColor;
            target.HoverOpacity = source.HoverOpacity;
            target.DropShadowOpacity = source.DropShadowOpacity;
            target.SelectedScreen = source.SelectedScreen;
            target.AutoCopyScreenshots = source.AutoCopyScreenshots;
            target.EnableMagnifier = source.EnableMagnifier;
            target.MagnifierZoomLevel = source.MagnifierZoomLevel;
            
            // Copy hotkeys
            target.Hotkeys.Clear();
            foreach (var kvp in source.Hotkeys)
            {
                target.Hotkeys[kvp.Key] = kvp.Value;
            }
        }

        private void PopulateScreenDropdown()
        {
            // Clear existing items
            ScreenComboBox.Items.Clear();
            
            // Get all screens
            var screens = System.Windows.Forms.Screen.AllScreens;
            
            // Add primary monitor option
            ScreenComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Primary Monitor" });
            
            // Add individual monitors
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                var isPrimary = screen.Primary;
                var monitorName = isPrimary ? $"Monitor {i + 1} (Primary)" : $"Monitor {i + 1}";
                ScreenComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = monitorName });
            }
            
            // Select the saved setting
            foreach (var item in ScreenComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && 
                    comboItem.Content.ToString() == _originalSettings.SelectedScreen)
                {
                    ScreenComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // If no match found, default to "Primary Monitor"
            if (ScreenComboBox.SelectedItem == null)
            {
                ScreenComboBox.SelectedIndex = 0;
            }
        }

        private void UpdateOpacityLabels()
        {
            HoverOpacityValue.Text = $"{(HoverOpacitySlider.Value * 100):F1}%";
            DropShadowOpacityValue.Text = $"{(DropShadowOpacitySlider.Value * 100):F1}%";
        }
        
        private void UpdateThemeColors()
        {
            try
            {
                var iconColor = _originalSettings.IconColor;
                if (string.IsNullOrEmpty(iconColor))
                    iconColor = "#FFFF8C00"; // Default orange
                
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(iconColor);
                var brush = new System.Windows.Media.SolidColorBrush(color);
                
                // Update colored text elements
                if (SettingsHeader != null)
                    SettingsHeader.Foreground = brush;
                if (ThemeCustomizationHeader != null)
                    ThemeCustomizationHeader.Foreground = brush;
                if (HotkeysHeader != null)
                    HotkeysHeader.Foreground = brush;
                
                // Update textbox borders
                if (SavePathTextBox != null)
                    SavePathTextBox.BorderBrush = brush;
                if (IconColorTextBox != null)
                    IconColorTextBox.BorderBrush = brush;
                
                // Update hotkey textbox borders
                if (ScreenshotRegionHotkeyTextBox != null)
                    ScreenshotRegionHotkeyTextBox.BorderBrush = brush;
                if (ScreenshotFullscreenHotkeyTextBox != null)
                    ScreenshotFullscreenHotkeyTextBox.BorderBrush = brush;
                if (RecordRegionHotkeyTextBox != null)
                    RecordRegionHotkeyTextBox.BorderBrush = brush;
                if (RecordFullscreenHotkeyTextBox != null)
                    RecordFullscreenHotkeyTextBox.BorderBrush = brush;
                if (SaveHotkeyTextBox != null)
                    SaveHotkeyTextBox.BorderBrush = brush;
                if (CopyHotkeyTextBox != null)
                    CopyHotkeyTextBox.BorderBrush = brush;
                
                // Update button borders and text colors
                if (CancelButton != null)
                {
                    CancelButton.BorderBrush = brush;
                    if (CancelButton.Content is TextBlock cancelText)
                        cancelText.Foreground = brush;
                }
                if (SaveButton != null)
                {
                    SaveButton.BorderBrush = brush;
                    if (SaveButton.Content is TextBlock saveText)
                        saveText.Foreground = brush;
                }
                
                // Update Browse button (find it by name)
                var browseButton = this.FindName("BrowseButton") as Button;
                if (browseButton != null)
                {
                    browseButton.BorderBrush = brush;
                    if (browseButton.Content is TextBlock browseText)
                        browseText.Foreground = brush;
                }
                
                // Update CloseSettingsButton (X button) foreground color
                if (CloseSettingsButton != null)
                {
                    CloseSettingsButton.Foreground = brush;
                }
                
                // Update ResizeGripBrush resource for the 3 squares in bottom right
                var resizeGripBrush = this.Resources["ResizeGripBrush"] as System.Windows.Media.SolidColorBrush;
                if (resizeGripBrush != null)
                {
                    resizeGripBrush.Color = color;
                }
                
                // Update ScrollBarBrush resource for scroll bar arrows and elements
                var scrollBarBrush = this.Resources["ScrollBarBrush"] as System.Windows.Media.SolidColorBrush;
                if (scrollBarBrush != null)
                {
                    scrollBarBrush.Color = color;
                }
                
                System.Diagnostics.Debug.WriteLine($"Updated settings theme colors: {iconColor}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update theme colors: {ex.Message}");
            }
        }

        private void ApplyThemeChanges()
        {
            try
            {
                // Get the main window to apply theme changes
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    // Apply the new theme settings
                    mainWindow.ApplyThemeSettings();
                    
                    // Update hotkeys if global hotkeys are enabled
                    if (_originalSettings.EnableGlobalHotkeys)
                    {
                        mainWindow.UpdateHotkeys();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply theme changes: {ex.Message}");
            }
        }
        
        private void IconColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var text = IconColorTextBox.Text;
                if (!string.IsNullOrEmpty(text) && text.StartsWith("#"))
                {
                    // Temporarily remove the event handler to avoid infinite loop
                    IconColorWheel.ColorChanged -= (s, color) => IconColorTextBox.Text = color;
                    
                    // Update the color wheel
                    IconColorWheel.SetColor(text);
                    
                    // Re-add the event handler
                    IconColorWheel.ColorChanged += (s, color) => IconColorTextBox.Text = color;
                }
            }
            catch
            {
                // Ignore invalid color formats
            }
        }

        private void LoadAudioDevices()
        {
            try
            {
                LogToFile("Loading audio devices...");
                var audioDevices = GetAvailableAudioDevices();
                
                LogToFile($"Found {audioDevices.Count} total audio devices");
                
                // Simple categorization based on device names
                var outputDevices = new List<string>();
                var inputDevices = new List<string>();
                
                foreach (var device in audioDevices)
                {
                    var isInput = IsInputDevice(device);
                    var isOutput = IsOutputDevice(device);
                    
                    if (isOutput)
                    {
                        outputDevices.Add(device);
                    }
                    if (isInput)
                    {
                        inputDevices.Add(device);
                    }
                }
                
                // Populate output audio devices
                OutputAudioDeviceComboBox.Items.Clear();
                OutputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Auto-detect" });
                
                foreach (var device in outputDevices)
                {
                    OutputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = device });
                }
                
                // Populate input audio devices
                InputAudioDeviceComboBox.Items.Clear();
                InputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Auto-detect" });
                
                foreach (var device in inputDevices)
                {
                    InputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = device });
                }
                
                LogToFile($"Found {outputDevices.Count} output devices: {string.Join(", ", outputDevices)}");
                LogToFile($"Found {inputDevices.Count} input devices: {string.Join(", ", inputDevices)}");
            }
            catch (Exception ex)
            {
                LogToFile($"Error loading audio devices: {ex.Message}");
            }
        }

        private void SetSelectedAudioDevices()
        {
            // Set output audio device
            foreach (var item in OutputAudioDeviceComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && 
                    comboItem.Content.ToString() == _originalSettings.SelectedOutputAudioDevice)
                {
                    OutputAudioDeviceComboBox.SelectedItem = item;
                    break;
                }
            }

            // Set input audio device
            foreach (var item in InputAudioDeviceComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && 
                    comboItem.Content.ToString() == _originalSettings.SelectedInputAudioDevice)
                {
                    InputAudioDeviceComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private bool IsInputDevice(string deviceName)
        {
            // ScreenRecorderLib handles device categorization automatically
            // Just check if it's not a system audio device
            var systemAudioKeywords = new[] { "stereo mix", "what u hear", "cable output", "vb-audio", "system audio" };
            
            foreach (var keyword in systemAudioKeywords)
            {
                if (deviceName.ToLower().Contains(keyword.ToLower()))
                {
                    return false; // This is an output device
                }
            }
            
            return true; // Assume it's an input device
        }

        private bool IsOutputDevice(string deviceName)
        {
            // ScreenRecorderLib handles device categorization automatically
            // Just check if it's a system audio device
            var systemAudioKeywords = new[] { "stereo mix", "what u hear", "cable output", "vb-audio", "system audio" };
            
            foreach (var keyword in systemAudioKeywords)
            {
                if (deviceName.ToLower().Contains(keyword.ToLower()))
                {
                    return true; // This is an output device
                }
            }
            
            return false; // Assume it's not an output device
        }

        private List<string> GetAvailableAudioDevices()
        {
            var devices = new List<string>();
            
            try
            {
                LogToFile("=== Getting available audio devices ===");
                
                // Note: ScreenRecorderLib API may have changed
                // For now, return empty list - audio device detection will be implemented later
                LogToFile("Audio device detection temporarily disabled - API changes detected");
                
                LogToFile($"Total audio devices found: {devices.Count}");
                return devices;
            }
            catch (Exception ex)
            {
                LogToFile($"Error getting audio devices: {ex.Message}");
                return devices;
            }
        }

        private string GetFFmpegPath()
        {
            // Look for FFmpeg in common locations
            var possiblePaths = new[]
            {
                "ffmpeg.exe",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    LogToFile($"Found FFmpeg at: {path}");
                    return path;
                }
            }

            LogToFile("FFmpeg not found in any location");
            return string.Empty;
        }

        private void RecordingEngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecordingEngineComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                _originalSettings.RecordingEngine = selectedItem.Content.ToString() ?? "ScreenRecorderLib";
            }
        }

        private void AudioRecordingModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AudioRecordingModeComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                var mode = selectedItem.Content.ToString();
                
                switch (mode)
                {
                    case "No Audio":
                        OutputAudioDevicePanel.Visibility = Visibility.Collapsed;
                        InputAudioDevicePanel.Visibility = Visibility.Collapsed;
                        break;
                    case "System Audio Only":
                        OutputAudioDevicePanel.Visibility = Visibility.Visible;
                        InputAudioDevicePanel.Visibility = Visibility.Collapsed;
                        break;
                    case "Microphone Only":
                        OutputAudioDevicePanel.Visibility = Visibility.Collapsed;
                        InputAudioDevicePanel.Visibility = Visibility.Visible;
                        break;
                    case "System Audio + Microphone":
                        OutputAudioDevicePanel.Visibility = Visibility.Visible;
                        InputAudioDevicePanel.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void RefreshAudioDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToFile("=== Refreshing Audio Devices ===");
                
                // Test FFmpeg command manually and show results
                var ffmpegPath = GetFFmpegPath();
                var ffmpegFound = !string.IsNullOrEmpty(ffmpegPath);
                
                var testResult = TestFFmpegAudioDevices();
                
                // Clear existing devices
                OutputAudioDeviceComboBox.Items.Clear();
                InputAudioDeviceComboBox.Items.Clear();
                
                // Get available devices
                var devices = GetAvailableAudioDevices();
                LogToFile($"Found {devices.Count} total devices");
                
                // Categorize devices
                var inputDevices = new List<string>();
                var outputDevices = new List<string>();
                
                foreach (var device in devices)
                {
                    LogToFile($"Processing device: {device}");
                    if (IsInputDevice(device))
                    {
                        inputDevices.Add(device);
                        LogToFile($"Added to input devices: {device}");
                    }
                    if (IsOutputDevice(device))
                    {
                        outputDevices.Add(device);
                        LogToFile($"Added to output devices: {device}");
                    }
                }
                
                LogToFile($"Categorized {inputDevices.Count} input devices and {outputDevices.Count} output devices");
                
                // Populate dropdowns
                foreach (var device in inputDevices)
                {
                    InputAudioDeviceComboBox.Items.Add(device);
                }
                
                foreach (var device in outputDevices)
                {
                    OutputAudioDeviceComboBox.Items.Add(device);
                }
                
                // Set selected devices
                SetSelectedAudioDevices();
                
                var message = $"FFmpeg found: {ffmpegFound}\n";
                message += $"FFmpeg path: {ffmpegPath}\n\n";
                message += $"FFmpeg test result: {testResult}\n\n";
                message += $"Found {inputDevices.Count} input devices and {outputDevices.Count} output devices.\n\n";
                message += $"Input devices: {string.Join(", ", inputDevices)}\n\n";
                message += $"Output devices: {string.Join(", ", outputDevices)}\n\n";
                message += "Check audio_debug.log for detailed information.";
                
                MessageBox.Show(message, "Audio Device Detection Results", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogToFile($"Error refreshing audio devices: {ex.Message}");
                MessageBox.Show($"Error refreshing audio devices: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string TestFFmpegAudioDevices()
        {
            try
            {
                var ffmpegPath = GetFFmpegPath();
                if (string.IsNullOrEmpty(ffmpegPath))
                {
                    LogToFile("FFmpeg not found for testing");
                    return "FFmpeg not found";
                }

                LogToFile("Testing FFmpeg audio device detection...");
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-list_devices true -f wasapi -i dummy",
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardError.ReadToEnd();
                var stdout = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                LogToFile($"FFmpeg test exit code: {process.ExitCode}");
                LogToFile($"FFmpeg test stderr: {output}");
                LogToFile($"FFmpeg test stdout: {stdout}");

                return $"Exit code: {process.ExitCode}, Output length: {output.Length}";
            }
            catch (Exception ex)
            {
                LogToFile($"Error testing FFmpeg: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private void LogToFile(string message)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio_debug.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
} 