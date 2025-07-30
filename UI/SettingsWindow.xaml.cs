using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SharpShot.Models;
using SharpShot.Services;
using SharpShot.Utils;
using System.Collections.Generic;
using System.Windows.Controls;

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
            
            LoadSettings();
            
            // Populate screen dropdown with actual monitors
            PopulateScreenDropdown();
            
            // Add event handlers for sliders
            HoverOpacitySlider.ValueChanged += (s, e) => UpdateOpacityLabels();
            DropShadowOpacitySlider.ValueChanged += (s, e) => UpdateOpacityLabels();
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
            
            AudioRecordingCheckBox.IsChecked = _originalSettings.EnableAudioRecording;
            GlobalHotkeysCheckBox.IsChecked = _originalSettings.EnableGlobalHotkeys;
            StartMinimizedCheckBox.IsChecked = _originalSettings.StartMinimized;
            
            // Load theme customization settings
            IconColorTextBox.Text = _originalSettings.IconColor;
            HoverOpacitySlider.Value = _originalSettings.HoverOpacity;
            DropShadowOpacitySlider.Value = _originalSettings.DropShadowOpacity;
            UpdateOpacityLabels();
            
            // Load hotkeys
            RegionHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("RegionCapture", "Double Ctrl");
            FullScreenHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("FullScreenCapture", "Ctrl+Shift+S");
            RecordingHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("ToggleRecording", "Ctrl+Shift+R");
            CancelHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("Cancel", "Escape");
            SaveHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("Save", "Space");
            CopyHotkeyTextBox.Text = _originalSettings.Hotkeys.GetValueOrDefault("Copy", "Enter");
            
            // Load triple-click settings
            RegionTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("RegionCaptureTripleClick", "false") == "true";
            FullScreenTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("FullScreenCaptureTripleClick", "false") == "true";
            RecordingTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("ToggleRecordingTripleClick", "false") == "true";
            CancelTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("CancelTripleClick", "false") == "true";
            SaveTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("SaveTripleClick", "false") == "true";
            CopyTripleClickCheckBox.IsChecked = _originalSettings.Hotkeys.GetValueOrDefault("CopyTripleClick", "false") == "true";
            
            // Apply current theme colors
            UpdateThemeColors();
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
            if (key == "System")
                return;

            var hotkey = modifiers.Count > 0 ? string.Join("+", modifiers) + "+" + key : key;
            
            if (_currentHotkeyTextBox != null)
            {
                _currentHotkeyTextBox.Text = hotkey;
                _currentHotkeyTextBox.IsReadOnly = true;
            }
            
            _isListeningForHotkey = false;
            _currentHotkeyTextBox = null;
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
                    _originalSettings.ScreenshotFormat = formatItem.Content.ToString();
                }
                
                if (QualityComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem qualityItem)
                {
                    _originalSettings.VideoQuality = qualityItem.Content.ToString();
                }
                
                if (ScreenComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem screenItem)
                {
                    _originalSettings.SelectedScreen = screenItem.Content.ToString();
                }
                
                _originalSettings.EnableAudioRecording = AudioRecordingCheckBox.IsChecked ?? false;
                _originalSettings.EnableGlobalHotkeys = GlobalHotkeysCheckBox.IsChecked ?? false;
                _originalSettings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
                
                // Save theme customization settings
                _originalSettings.IconColor = IconColorTextBox.Text;
                _originalSettings.HoverOpacity = HoverOpacitySlider.Value;
                _originalSettings.DropShadowOpacity = DropShadowOpacitySlider.Value;
                
                // Update theme colors immediately
                UpdateThemeColors();
                
                // Update hotkeys
                _originalSettings.Hotkeys["RegionCapture"] = RegionHotkeyTextBox.Text;
                _originalSettings.Hotkeys["FullScreenCapture"] = FullScreenHotkeyTextBox.Text;
                _originalSettings.Hotkeys["ToggleRecording"] = RecordingHotkeyTextBox.Text;
                _originalSettings.Hotkeys["Cancel"] = CancelHotkeyTextBox.Text;
                _originalSettings.Hotkeys["Save"] = SaveHotkeyTextBox.Text;
                _originalSettings.Hotkeys["Copy"] = CopyHotkeyTextBox.Text;
                
                // Update triple-click settings
                _originalSettings.Hotkeys["RegionCaptureTripleClick"] = RegionTripleClickCheckBox.IsChecked == true ? "true" : "false";
                _originalSettings.Hotkeys["FullScreenCaptureTripleClick"] = FullScreenTripleClickCheckBox.IsChecked == true ? "true" : "false";
                _originalSettings.Hotkeys["ToggleRecordingTripleClick"] = RecordingTripleClickCheckBox.IsChecked == true ? "true" : "false";
                _originalSettings.Hotkeys["CancelTripleClick"] = CancelTripleClickCheckBox.IsChecked == true ? "true" : "false";
                _originalSettings.Hotkeys["SaveTripleClick"] = SaveTripleClickCheckBox.IsChecked == true ? "true" : "false";
                _originalSettings.Hotkeys["CopyTripleClick"] = CopyTripleClickCheckBox.IsChecked == true ? "true" : "false";
                
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
            target.EnableAudioRecording = source.EnableAudioRecording;
            target.EnableGlobalHotkeys = source.EnableGlobalHotkeys;
            target.StartMinimized = source.StartMinimized;
            target.IconColor = source.IconColor;
            target.HoverOpacity = source.HoverOpacity;
            target.DropShadowOpacity = source.DropShadowOpacity;
            target.SelectedScreen = source.SelectedScreen;
            
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
            
            // Add "All Monitors" option
            ScreenComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "All Monitors" });
            
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
            
            // If no match found, default to "All Monitors"
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
                if (RegionHotkeyTextBox != null)
                    RegionHotkeyTextBox.BorderBrush = brush;
                if (FullScreenHotkeyTextBox != null)
                    FullScreenHotkeyTextBox.BorderBrush = brush;
                if (RecordingHotkeyTextBox != null)
                    RecordingHotkeyTextBox.BorderBrush = brush;
                if (CancelHotkeyTextBox != null)
                    CancelHotkeyTextBox.BorderBrush = brush;
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
    }
} 