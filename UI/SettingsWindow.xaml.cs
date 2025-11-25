using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SharpShot.Models;
using SharpShot.Services;
using SharpShot.Utils;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System.Management;

// Windows Core Audio API imports
[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
class MMDeviceEnumerator { }

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    void NotNeeded();
    [PreserveSig]
    int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr ppDevices);
    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr ppEndpoint);
    [PreserveSig]
    int GetDevice(string pwstrId, out IntPtr ppDevice);
    [PreserveSig]
    int RegisterEndpointNotificationCallback(IntPtr pClient);
    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IntPtr pClient);
}

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume
{
    void NotNeeded();
    [PreserveSig]
    int NotNeeded2();
    [PreserveSig]
    int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
    [PreserveSig]
    int VolumeStepUp(out Guid pguidEventContext);
    [PreserveSig]
    int VolumeStepDown(out Guid pguidEventContext);
    [PreserveSig]
    int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
    [PreserveSig]
    int GetMasterVolumeLevelScalar(out float pfLevel);
    [PreserveSig]
    int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
    [PreserveSig]
    int GetMasterVolumeLevel(out float pfLevelDB);
    [PreserveSig]
    int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
}

[Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioClient
{
    void NotNeeded();
    [PreserveSig]
    int Initialize(int ShareMode, uint StreamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, ref Guid AudioSessionGuid);
    [PreserveSig]
    int GetBufferSize(out uint pNumBufferFrames);
    [PreserveSig]
    int GetStreamLatency(out long phnsLatency);
    [PreserveSig]
    int GetCurrentPadding(out uint pNumPaddingFrames);
    [PreserveSig]
    int IsFormatSupported(int bIsSupported, IntPtr pFormat, out IntPtr ppClosestMatch);
    [PreserveSig]
    int GetMixFormat(out IntPtr ppDeviceFormat);
    [PreserveSig]
    int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
    [PreserveSig]
    int Start();
    [PreserveSig]
    int Stop();
    [PreserveSig]
    int Reset();
    [PreserveSig]
    int SetEventHandle(IntPtr eventHandle);
    [PreserveSig]
    int GetService(ref Guid riid, out IntPtr ppv);
}

[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    void NotNeeded();
    [PreserveSig]
    int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
    [PreserveSig]
    int OpenPropertyStore(uint stgmAccess, out IntPtr ppProperties);
    [PreserveSig]
    int GetId(out string ppstrId);
    [PreserveSig]
    int GetState(out uint pdwState);
}

[Guid("2EDEF04C-8C67-4F1F-8AFC-8DC0F5D8C0B3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceCollection
{
    void NotNeeded();
    [PreserveSig]
    int GetCount(out uint pcDevices);
    [PreserveSig]
    int Item(uint nDevice, out IntPtr ppDevice);
}

[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPropertyStore
{
    void NotNeeded();
    [PreserveSig]
    int GetCount(out uint cProps);
    [PreserveSig]
    int GetAt(uint iProp, out PROPERTYKEY pkey);
    [PreserveSig]
    int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
    [PreserveSig]
    int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
    [PreserveSig]
    int Commit();
}

[StructLayout(LayoutKind.Sequential)]
struct PROPERTYKEY
{
    public Guid fmtid;
    public uint pid;
}

[StructLayout(LayoutKind.Sequential)]
struct PROPVARIANT
{
    public ushort vt;
    public ushort wReserved1;
    public ushort wReserved2;
    public ushort wReserved3;
    public IntPtr data;
}

// Windows Core Audio API constants
static class WindowsCoreAudioAPI
{
    public const int eConsole = 0;
    public const int eMultimedia = 1;
    public const int eCommunications = 2;
    public const int eRender = 0;
    public const int eCapture = 1;
    public const int eAll = 2;
    public const int DEVICE_STATE_ACTIVE = 0x1;
    public const int DEVICE_STATE_DISABLED = 0x2;
    public const int DEVICE_STATE_NOTPRESENT = 0x4;
    public const int DEVICE_STATE_UNPLUGGED = 0x8;
    public const int DEVICE_STATEMASK_ALL = 0xf;

    [DllImport("ole32.dll")]
    public static extern int CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid riid, out IntPtr ppv);

    [DllImport("ole32.dll")]
    public static extern int CoInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    public static extern void CoUninitialize();

    [DllImport("oleaut32.dll")]
    public static extern int VariantClear(IntPtr pvarg);
}

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
            
            // Populate editor display monitor dropdown
            PopulateEditorDisplayMonitorDropdown();
            
            // Populate magnifier stationary monitor dropdown BEFORE loading settings
            PopulateMagnifierStationaryMonitorDropdown();
            
            LoadSettings();
            
            // Add event handlers for sliders
            HoverOpacitySlider.ValueChanged += (s, e) => 
            {
                UpdateOpacityLabels();
                // Update in real-time
                _originalSettings.HoverOpacity = HoverOpacitySlider.Value;
                ApplyOpacityChanges();
            };
            DropShadowOpacitySlider.ValueChanged += (s, e) => 
            {
                UpdateOpacityLabels();
                // Update in real-time
                _originalSettings.DropShadowOpacity = DropShadowOpacitySlider.Value;
                ApplyOpacityChanges();
            };
            
            // Add event handler for magnifier checkbox
            EnableMagnifierCheckBox.Checked += (s, e) => 
            {
                MagnifierZoomPanel.Visibility = Visibility.Visible;
                MagnifierModePanel.Visibility = Visibility.Visible;
                UpdateMagnifierStationaryPanelVisibility();
            };
            EnableMagnifierCheckBox.Unchecked += (s, e) => 
            {
                MagnifierZoomPanel.Visibility = Visibility.Collapsed;
                MagnifierModePanel.Visibility = Visibility.Collapsed;
                MagnifierStationaryPanel.Visibility = Visibility.Collapsed;
            };
            
            // Add event handlers for magnifier mode
            if (MagnifierModeComboBox != null)
            {
                MagnifierModeComboBox.SelectionChanged += MagnifierModeComboBox_SelectionChanged;
            }
            
            // Populate auto mode monitor list
            PopulateMagnifierAutoMonitorList();
            
            // Populate boundary box list
            PopulateMagnifierBoundaryBoxList();
            
            // Add event handlers for stationary position sliders
            if (MagnifierStationaryXSlider != null)
            {
                MagnifierStationaryXSlider.ValueChanged += (s, e) => 
                {
                    if (MagnifierStationaryXText != null)
                        MagnifierStationaryXText.Text = ((int)MagnifierStationaryXSlider.Value).ToString();
                };
            }
            if (MagnifierStationaryYSlider != null)
            {
                MagnifierStationaryYSlider.ValueChanged += (s, e) => 
                {
                    if (MagnifierStationaryYText != null)
                        MagnifierStationaryYText.Text = ((int)MagnifierStationaryYSlider.Value).ToString();
                };
            }
            
            // Add event handler for color wheel text box
            IconColorTextBox.TextChanged += IconColorTextBox_TextChanged;
            
            // Set initial visibility
            bool magnifierEnabled = _originalSettings.EnableMagnifier;
            MagnifierZoomPanel.Visibility = magnifierEnabled ? Visibility.Visible : Visibility.Collapsed;
            MagnifierModePanel.Visibility = magnifierEnabled ? Visibility.Visible : Visibility.Collapsed;
            UpdateMagnifierStationaryPanelVisibility();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging the window by clicking anywhere on it
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    // Ignore drag move errors - they can occur when the window is in certain states
                    // This prevents crashes when clicking the title bar
                }
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
            
            // Set editor display monitor combo box
            foreach (var item in EditorDisplayMonitorComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && 
                    comboItem.Content.ToString() == _originalSettings.ScreenshotEditorDisplayMonitor)
                {
                    EditorDisplayMonitorComboBox.SelectedItem = item;
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
            
            // Audio recording mode combo box commented out
            // foreach (var item in AudioRecordingModeComboBox.Items)
            // {
            //     if (item is System.Windows.Controls.ComboBoxItem comboItem && 
            //         comboItem.Content.ToString() == _originalSettings.AudioRecordingMode)
            //     {
            //         AudioRecordingModeComboBox.SelectedItem = item;
            //         break;
            //     }
            // }

            // Load audio devices after UI is fully initialized
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    LogToFile("Starting automatic audio device loading after UI initialization...");
                    LoadAudioDevices();
                    SetSelectedAudioDevices();
                    LogToFile("Audio device loading completed successfully");
                }
                catch (Exception ex)
                {
                    LogToFile($"Error during automatic audio device loading: {ex.Message}");
                    // Don't let audio device loading failure prevent settings from loading
                }
            }));
            GlobalHotkeysCheckBox.IsChecked = _originalSettings.EnableGlobalHotkeys;
            StartMinimizedCheckBox.IsChecked = _originalSettings.StartMinimized;
            AutoCopyScreenshotsCheckBox.IsChecked = _originalSettings.AutoCopyScreenshots;
            SkipEditorAndAutoCopyCheckBox.IsChecked = _originalSettings.SkipEditorAndAutoCopy;
            EnableMagnifierCheckBox.IsChecked = _originalSettings.EnableMagnifier;
            DisableAllPopupsCheckBox.IsChecked = _originalSettings.DisableAllPopups;
            
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
            
            // Load magnifier size (stationary)
            if (MagnifierSizeSlider != null)
            {
                MagnifierSizeSlider.Value = _originalSettings.MagnifierSize;
                UpdateMagnifierSizeText();
            }
            
            // Add event handler for magnifier size slider (stationary)
            if (MagnifierSizeSlider != null)
            {
                MagnifierSizeSlider.ValueChanged += (s, e) => UpdateMagnifierSizeText();
            }
            
            // Load magnifier follow size
            if (MagnifierFollowSizeSlider != null)
            {
                MagnifierFollowSizeSlider.Value = _originalSettings.MagnifierFollowSize;
                UpdateMagnifierFollowSizeText();
            }
            
            // Add event handler for magnifier follow size slider
            if (MagnifierFollowSizeSlider != null)
            {
                MagnifierFollowSizeSlider.ValueChanged += (s, e) => UpdateMagnifierFollowSizeText();
            }
            
            // Load magnifier mode
            if (MagnifierModeComboBox != null)
            {
                string mode = _originalSettings.MagnifierMode ?? "Follow";
                foreach (System.Windows.Controls.ComboBoxItem item in MagnifierModeComboBox.Items)
                {
                    if (item.Tag?.ToString() == mode)
                    {
                        MagnifierModeComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
            
            // Load stationary settings
            if (MagnifierStationaryXSlider != null)
            {
                // Ensure Maximum is explicitly set before setting Value
                MagnifierStationaryXSlider.Maximum = 100;
                MagnifierStationaryXSlider.Minimum = 0;
                double xValue = Math.Max(0, Math.Min(100, _originalSettings.MagnifierStationaryX));
                MagnifierStationaryXSlider.Value = xValue;
                // Update text display immediately
                if (MagnifierStationaryXText != null)
                {
                    MagnifierStationaryXText.Text = ((int)xValue).ToString();
                }
            }
            if (MagnifierStationaryYSlider != null)
            {
                // Ensure Maximum is explicitly set before setting Value
                MagnifierStationaryYSlider.Maximum = 100;
                MagnifierStationaryYSlider.Minimum = 0;
                double yValue = Math.Max(0, Math.Min(100, _originalSettings.MagnifierStationaryY));
                MagnifierStationaryYSlider.Value = yValue;
                // Update text display immediately
                if (MagnifierStationaryYText != null)
                {
                    MagnifierStationaryYText.Text = ((int)yValue).ToString();
                }
            }
            
            // Load monitor selection - ensure dropdown is populated and has items
            if (MagnifierStationaryMonitorComboBox != null && MagnifierStationaryMonitorComboBox.Items.Count > 0)
            {
                string monitor = _originalSettings.MagnifierStationaryMonitor ?? "Primary Monitor";
                bool found = false;
                
                // Try exact match first
                foreach (System.Windows.Controls.ComboBoxItem item in MagnifierStationaryMonitorComboBox.Items)
                {
                    if (item.Content?.ToString() == monitor)
                    {
                        MagnifierStationaryMonitorComboBox.SelectedItem = item;
                        found = true;
                        System.Diagnostics.Debug.WriteLine($"Found and selected monitor: {monitor}");
                        break;
                    }
                }
                
                // If not found, default to "Primary Monitor"
                if (!found)
                {
                    System.Diagnostics.Debug.WriteLine($"Monitor '{monitor}' not found in dropdown, defaulting to Primary Monitor");
                    foreach (System.Windows.Controls.ComboBoxItem item in MagnifierStationaryMonitorComboBox.Items)
                    {
                        if (item.Content?.ToString() == "Primary Monitor")
                        {
                            MagnifierStationaryMonitorComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            else if (MagnifierStationaryMonitorComboBox != null)
            {
                System.Diagnostics.Debug.WriteLine("MagnifierStationaryMonitorComboBox is null or has no items");
            }
            
            // Load theme customization settings
            IconColorTextBox.Text = _originalSettings.IconColor;
            IconColorWheel.SetColor(_originalSettings.IconColor);
            IconColorWheel.ColorChanged += (s, color) => 
            {
                IconColorTextBox.Text = color;
                UpdateThemeColors();
            };
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
            
            // Apply current hotkeys immediately so they work without saving
            ApplyCurrentHotkeys();
            
            // Apply initial theme colors and close button style
            UpdateThemeColors();
            
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
                
                if (EditorDisplayMonitorComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem editorMonitorItem)
                {
                    _originalSettings.ScreenshotEditorDisplayMonitor = editorMonitorItem.Content?.ToString() ?? "Primary Monitor";
                }
                
                if (RecordingEngineComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem recordingEngineItem)
                {
                    var engine = recordingEngineItem.Content?.ToString() ?? "FFmpeg";
                    _originalSettings.RecordingEngine = engine;
                    
                    // Show OBS-specific settings if OBS is selected
                    if (engine == "OBS")
                    {
                        ShowOBSSettings();
                    }
                    else
                    {
                        HideOBSSettings();
                    }
                }
                
                // Audio recording mode combo box commented out
                // if (AudioRecordingModeComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem audioModeItem)
                // {
                //     _originalSettings.AudioRecordingMode = audioModeItem.Content?.ToString() ?? "No Audio";
                // }
                
                // Default to "No Audio" since combo box is commented out
                _originalSettings.AudioRecordingMode = "No Audio";

                // Save selected audio devices
                if (OutputAudioDeviceComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem outputAudioItem)
                {
                    var outputDevice = outputAudioItem.Content?.ToString() ?? string.Empty;
                    _originalSettings.SelectedOutputAudioDevice = outputDevice;
                    LogToFile($"Saving output audio device: '{outputDevice}'");
                }
                else
                {
                    LogToFile("No output audio device selected");
                }

                if (InputAudioDeviceComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem inputAudioItem)
                {
                    var inputDevice = inputAudioItem.Content?.ToString() ?? string.Empty;
                    _originalSettings.SelectedInputAudioDevice = inputDevice;
                    LogToFile($"Saving input audio device: '{inputDevice}'");
                }
                else
                {
                    LogToFile("No input audio device selected");
                }
                _originalSettings.EnableGlobalHotkeys = GlobalHotkeysCheckBox.IsChecked ?? false;
                _originalSettings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
                _originalSettings.AutoCopyScreenshots = AutoCopyScreenshotsCheckBox.IsChecked ?? false;
                _originalSettings.SkipEditorAndAutoCopy = SkipEditorAndAutoCopyCheckBox.IsChecked ?? false;
                _originalSettings.EnableMagnifier = EnableMagnifierCheckBox.IsChecked ?? false;
                _originalSettings.DisableAllPopups = DisableAllPopupsCheckBox.IsChecked ?? false;
                
                // Save magnifier zoom level
                if (MagnifierZoomComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem zoomItem)
                {
                    var zoomText = zoomItem.Content?.ToString() ?? "2.0x";
                    if (double.TryParse(zoomText.Replace("x", ""), out double zoomLevel))
                    {
                        _originalSettings.MagnifierZoomLevel = zoomLevel;
                    }
                }
                
                // Save magnifier size (stationary)
                if (MagnifierSizeSlider != null)
                {
                    _originalSettings.MagnifierSize = (int)MagnifierSizeSlider.Value;
                }
                
                // Save magnifier follow size
                if (MagnifierFollowSizeSlider != null)
                {
                    _originalSettings.MagnifierFollowSize = (int)MagnifierFollowSizeSlider.Value;
                }
                
                // Save magnifier mode
                if (MagnifierModeComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem modeItem)
                {
                    _originalSettings.MagnifierMode = modeItem.Tag?.ToString() ?? "Follow";
                }
                
                // Save stationary settings
                if (MagnifierStationaryXSlider != null)
                {
                    _originalSettings.MagnifierStationaryX = MagnifierStationaryXSlider.Value;
                }
                if (MagnifierStationaryYSlider != null)
                {
                    _originalSettings.MagnifierStationaryY = MagnifierStationaryYSlider.Value;
                }
                if (MagnifierStationaryMonitorComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem monitorItem)
                {
                    string selectedMonitor = monitorItem.Content?.ToString() ?? "Primary Monitor";
                    _originalSettings.MagnifierStationaryMonitor = selectedMonitor;
                    System.Diagnostics.Debug.WriteLine($"Saving magnifier stationary monitor: {selectedMonitor}");
                }
                else
                {
                    // If no selection, default to Primary Monitor
                    _originalSettings.MagnifierStationaryMonitor = "Primary Monitor";
                    System.Diagnostics.Debug.WriteLine("No monitor selected, defaulting to Primary Monitor");
                }
                
                // Save auto mode monitor selections (including boundary boxes)
                if (MagnifierAutoMonitorList != null)
                {
                    var selectedMonitors = new List<string>();
                    foreach (System.Windows.Controls.CheckBox checkBox in MagnifierAutoMonitorList.Children.OfType<System.Windows.Controls.CheckBox>())
                    {
                        if (checkBox.IsChecked == true)
                        {
                            string monitorId = checkBox.Tag?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(monitorId))
                            {
                                selectedMonitors.Add(monitorId);
                            }
                        }
                    }
                    _originalSettings.MagnifierAutoStationaryMonitors = selectedMonitors;
                    System.Diagnostics.Debug.WriteLine($"Saving auto mode stationary monitors: {string.Join(", ", selectedMonitors)}");
                    
                    // Also update boundary box Enabled states based on checkboxes
                    var boundaryBoxes = _originalSettings.MagnifierBoundaryBoxes ?? new List<Models.MagnifierBoundaryBox>();
                    foreach (var box in boundaryBoxes)
                    {
                        var boxId = $"BoundaryBox:{box.Name}";
                        box.Enabled = selectedMonitors.Contains(boxId);
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
            target.SkipEditorAndAutoCopy = source.SkipEditorAndAutoCopy;
            target.EnableMagnifier = source.EnableMagnifier;
            target.DisableAllPopups = source.DisableAllPopups;
            target.MagnifierZoomLevel = source.MagnifierZoomLevel;
            target.MagnifierSize = source.MagnifierSize;
            target.MagnifierFollowSize = source.MagnifierFollowSize;
            target.MagnifierMode = source.MagnifierMode;
            target.MagnifierStationaryMonitor = source.MagnifierStationaryMonitor;
            target.MagnifierStationaryX = source.MagnifierStationaryX;
            target.MagnifierStationaryY = source.MagnifierStationaryY;
            target.MagnifierAutoStationaryMonitors = source.MagnifierAutoStationaryMonitors != null 
                ? new List<string>(source.MagnifierAutoStationaryMonitors) 
                : new List<string>();
            target.MagnifierBoundaryBoxes = source.MagnifierBoundaryBoxes != null
                ? new List<Models.MagnifierBoundaryBox>(source.MagnifierBoundaryBoxes.Select(b => new Models.MagnifierBoundaryBox
                {
                    Name = b.Name,
                    MonitorId = b.MonitorId,
                    Bounds = b.Bounds,
                    Enabled = b.Enabled
                }))
                : new List<Models.MagnifierBoundaryBox>();
            target.ScreenshotEditorDisplayMonitor = source.ScreenshotEditorDisplayMonitor;
            
            // Copy hotkeys
            target.Hotkeys.Clear();
            foreach (var kvp in source.Hotkeys)
            {
                target.Hotkeys[kvp.Key] = kvp.Value;
            }
        }

        private void ApplyCurrentHotkeys()
        {
            try
            {
                // Copy current hotkey settings to the service immediately
                var currentSettings = _settingsService.CurrentSettings;
                currentSettings.EnableGlobalHotkeys = _originalSettings.EnableGlobalHotkeys;
                
                // Copy hotkeys
                currentSettings.Hotkeys.Clear();
                foreach (var kvp in _originalSettings.Hotkeys)
                {
                    currentSettings.Hotkeys[kvp.Key] = kvp.Value;
                }
                
                // Update hotkeys immediately so they work without saving
                _hotkeyManager?.UpdateHotkeys();
                
                System.Diagnostics.Debug.WriteLine("Current hotkeys applied immediately - no save required");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying current hotkeys: {ex.Message}");
            }
        }

        private void PopulateScreenDropdown()
        {
            // Clear existing items
            ScreenComboBox.Items.Clear();
            
            // Get all screens
            var screens = System.Windows.Forms.Screen.AllScreens;
            
            // Add "All Screens" option first
            ScreenComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "All Screens" });
            
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

        private void PopulateMagnifierStationaryMonitorDropdown()
        {
            if (MagnifierStationaryMonitorComboBox == null) return;
            
            // Clear existing items
            MagnifierStationaryMonitorComboBox.Items.Clear();
            
            // Get all screens
            var screens = System.Windows.Forms.Screen.AllScreens;
            
            // Add primary monitor option first
            MagnifierStationaryMonitorComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Primary Monitor" });
            
            // Add individual monitors
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                var isPrimary = screen.Primary;
                var monitorName = isPrimary ? $"Monitor {i + 1} (Primary)" : $"Monitor {i + 1}";
                MagnifierStationaryMonitorComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = monitorName });
            }
            
            // If no selection exists, select "Primary Monitor" by default
            if (MagnifierStationaryMonitorComboBox.SelectedItem == null && MagnifierStationaryMonitorComboBox.Items.Count > 0)
            {
                MagnifierStationaryMonitorComboBox.SelectedIndex = 0;
            }
        }
        
        private void UpdateMagnifierSizeText()
        {
            if (MagnifierSizeSlider != null && MagnifierSizeText != null)
            {
                MagnifierSizeText.Text = $"{(int)MagnifierSizeSlider.Value}px";
            }
        }
        
        private void UpdateMagnifierFollowSizeText()
        {
            if (MagnifierFollowSizeSlider != null && MagnifierFollowSizeText != null)
            {
                MagnifierFollowSizeText.Text = $"{(int)MagnifierFollowSizeSlider.Value}px";
            }
        }
        
        private void NestedScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Don't handle mouse wheel events in nested ScrollViewers
            // Instead, let the main ScrollViewer handle them
            // Find the main ScrollViewer and scroll it instead
            var mainScrollViewer = FindMainScrollViewer(this);
            if (mainScrollViewer != null)
            {
                double offset = mainScrollViewer.VerticalOffset - (e.Delta / 3.0);
                double newOffset = Math.Max(0, Math.Min(offset, mainScrollViewer.ScrollableHeight));
                mainScrollViewer.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
        }
        
        private ScrollViewer FindMainScrollViewer(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer scrollViewer)
                {
                    // Check if this is the main ScrollViewer (has Grid.Row="1" or is at the root level)
                    if (scrollViewer.Parent is Grid grid && Grid.GetRow(scrollViewer) == 1)
                    {
                        return scrollViewer;
                    }
                }
                var result = FindMainScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }
        
        private void UpdateMagnifierStationaryPanelVisibility()
        {
            if (MagnifierModeComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                string mode = selectedItem.Tag?.ToString() ?? "Follow";
                bool showStationary = (mode == "Stationary" || mode == "Auto");
                bool showAutoMode = (mode == "Auto");
                
                MagnifierStationaryPanel.Visibility = showStationary ? Visibility.Visible : Visibility.Collapsed;
                MagnifierAutoModePanel.Visibility = showAutoMode ? Visibility.Visible : Visibility.Collapsed;
                
                // Show stationary size slider only for Stationary mode, follow size slider for Follow/Auto
                if (MagnifierSizeStationaryPanel != null)
                {
                    MagnifierSizeStationaryPanel.Visibility = (mode == "Stationary") ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            else
            {
                MagnifierStationaryPanel.Visibility = Visibility.Collapsed;
                MagnifierAutoModePanel.Visibility = Visibility.Collapsed;
            }
        }
        
        private void MagnifierModeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateMagnifierStationaryPanelVisibility();
        }
        
        private void PopulateMagnifierAutoMonitorList()
        {
            if (MagnifierAutoMonitorList == null) return;
            
            // Clear existing items
            MagnifierAutoMonitorList.Children.Clear();
            
            // Get all screens
            var screens = System.Windows.Forms.Screen.AllScreens;
            
            // Get currently selected monitors from settings
            var selectedMonitors = _originalSettings.MagnifierAutoStationaryMonitors ?? new List<string>();
            
            // Add checkbox for each monitor
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                var isPrimary = screen.Primary;
                var monitorName = isPrimary ? $"Monitor {i + 1} (Primary)" : $"Monitor {i + 1}";
                var monitorId = $"Monitor {i + 1}"; // Use consistent ID format
                
                var checkBox = new System.Windows.Controls.CheckBox
                {
                    Content = monitorName,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 5, 0, 5),
                    IsChecked = selectedMonitors.Contains(monitorId) || selectedMonitors.Contains(monitorName)
                };
                
                // Store monitor identifier in Tag for easy retrieval
                checkBox.Tag = monitorId;
                
                MagnifierAutoMonitorList.Children.Add(checkBox);
            }
            
            // Add checkboxes for boundary boxes
            var boundaryBoxes = _originalSettings.MagnifierBoundaryBoxes ?? new List<Models.MagnifierBoundaryBox>();
            foreach (var box in boundaryBoxes)
            {
                // Use the boundary box name as the identifier
                var boxId = $"BoundaryBox:{box.Name}";
                
                var checkBox = new System.Windows.Controls.CheckBox
                {
                    Content = box.Name,
                    Foreground = System.Windows.Media.Brushes.LightBlue,
                    Margin = new Thickness(15, 5, 0, 5), // Indent boundary boxes slightly
                    IsChecked = selectedMonitors.Contains(boxId) || box.Enabled
                };
                
                // Store boundary box identifier in Tag
                checkBox.Tag = boxId;
                
                // Update the box's Enabled property when checkbox is toggled
                checkBox.Checked += (s, e) => 
                {
                    box.Enabled = true;
                    // Also add to selected monitors list if not already there
                    if (!selectedMonitors.Contains(boxId))
                    {
                        selectedMonitors.Add(boxId);
                    }
                };
                checkBox.Unchecked += (s, e) => 
                {
                    box.Enabled = false;
                    // Remove from selected monitors list
                    selectedMonitors.Remove(boxId);
                };
                
                MagnifierAutoMonitorList.Children.Add(checkBox);
            }
        }
        
        private void PopulateMagnifierBoundaryBoxList()
        {
            if (MagnifierBoundaryBoxList == null) return;
            
            // Clear existing items
            MagnifierBoundaryBoxList.Children.Clear();
            
            var boundaryBoxes = _originalSettings.MagnifierBoundaryBoxes ?? new List<Models.MagnifierBoundaryBox>();
            var screens = System.Windows.Forms.Screen.AllScreens;
            
            foreach (var box in boundaryBoxes)
            {
                var panel = CreateBoundaryBoxPanel(box, screens);
                MagnifierBoundaryBoxList.Children.Add(panel);
            }
        }
        
        private void UpdateSliderColors(System.Windows.Media.Color themeColor)
        {
            try
            {
                // Update all sliders by finding their visual elements and updating colors
                UpdateSliderVisualTree(HoverOpacitySlider, themeColor);
                UpdateSliderVisualTree(DropShadowOpacitySlider, themeColor);
                UpdateSliderVisualTree(MagnifierSizeSlider, themeColor);
                UpdateSliderVisualTree(MagnifierFollowSizeSlider, themeColor);
                UpdateSliderVisualTree(MagnifierStationaryXSlider, themeColor);
                UpdateSliderVisualTree(MagnifierStationaryYSlider, themeColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating slider colors: {ex.Message}");
            }
        }
        
        private void UpdateSliderVisualTree(Slider slider, System.Windows.Media.Color themeColor)
        {
            if (slider == null) return;
            
            try
            {
                // Force template to be applied
                slider.ApplyTemplate();
                
                // Find the Track
                var track = slider.Template?.FindName("PART_Track", slider) as Track;
                if (track == null) return;
                
                // Update DecreaseRepeatButton (filled portion) - this uses DynamicResource so it should update automatically
                // But we can force it here too
                if (track.DecreaseRepeatButton is RepeatButton decreaseBtn)
                {
                    decreaseBtn.Background = new SolidColorBrush(themeColor);
                }
                
                // Update Thumb
                if (track.Thumb is Thumb thumb)
                {
                    // Find the Border in the thumb template
                    thumb.ApplyTemplate();
                    if (thumb.Template?.FindName("ThumbBorder", thumb) is Border thumbBorder)
                    {
                        thumbBorder.Background = new SolidColorBrush(themeColor);
                        thumbBorder.BorderBrush = new SolidColorBrush(themeColor);
                        // Update DropShadowEffect colors (both normal and hover)
                        if (thumbBorder.Effect is DropShadowEffect effect)
                        {
                            effect.Color = themeColor;
                        }
                        // Also check for hover effect if it exists
                        var hoverEffect = thumb.Template?.FindName("ThumbHoverDropShadow", thumb) as DropShadowEffect;
                        if (hoverEffect != null)
                        {
                            hoverEffect.Color = themeColor;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating slider visual tree: {ex.Message}");
            }
        }
        
        private void FindAndUpdateButtons(DependencyObject parent, System.Windows.Media.Color color, System.Windows.Media.SolidColorBrush brush)
        {
            try
            {
                if (parent == null) return;
                
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child == null) continue;
                    
                    if (child is Button btn)
                    {
                        // Check if button content is a TextBlock with Edit or Delete text
                        if (btn.Content is TextBlock text && (text.Text == "Edit" || text.Text == "Delete"))
                        {
                            try
                            {
                                btn.Style = CreateModernButtonStyle(color, 60.0, 32.0, allowDynamicWidth: true);
                                text.Foreground = brush;
                                // Re-apply MinWidth and MaxWidth after style is set
                                btn.MinWidth = 50;
                                btn.MaxWidth = 80;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error updating button style: {ex.Message}");
                            }
                        }
                    }
                    else if (child is Panel || child is ContentControl)
                    {
                        FindAndUpdateButtons(child, color, brush);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in FindAndUpdateButtons: {ex.Message}");
            }
        }
        
        private Panel CreateBoundaryBoxPanel(Models.MagnifierBoundaryBox box, System.Windows.Forms.Screen[] screens)
        {
            // Use Grid for better responsive layout
            var grid = new Grid
            {
                Margin = new Thickness(0, 5, 0, 5)
            };
            
            // Define columns: checkbox (auto), name (120), bounds (140*), buttons (auto)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Checkbox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // Name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Bounds (flexible)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Buttons container
            
            var checkBox = new CheckBox
            {
                IsChecked = box.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            checkBox.Checked += (s, e) => box.Enabled = true;
            checkBox.Unchecked += (s, e) => box.Enabled = false;
            Grid.SetColumn(checkBox, 0);
            
            var nameText = new TextBlock
            {
                Text = box.Name + ":",
                Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(nameText, 1);
            
            var boundsText = new TextBlock
            {
                Text = $"{box.Bounds.X},{box.Bounds.Y} {box.Bounds.Width}x{box.Bounds.Height}",
                Foreground = System.Windows.Media.Brushes.LightGray,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(boundsText, 2);
            
            // Container for buttons that will shrink together
            var buttonContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(buttonContainer, 3);
            
            var editButton = new Button
            {
                MinWidth = 50,
                MaxWidth = 80,
                Height = 32,
                Margin = new Thickness(5, 0, 5, 0),
                Style = CreateModernButtonStyle(System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00), 60.0, 32.0, allowDynamicWidth: true)
            };
            var editText = new TextBlock
            {
                Text = "Edit",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            editButton.Content = editText;
            
            var deleteButton = new Button
            {
                Height = 32,
                Margin = new Thickness(5, 0, 5, 0),
                Style = CreateModernButtonStyle(System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00), 60.0, 32.0, allowDynamicWidth: true)
            };
            // Set width constraints after style to ensure they're applied
            deleteButton.MinWidth = 50;
            deleteButton.MaxWidth = 80;
            var deleteText = new TextBlock
            {
                Text = "Delete",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            deleteButton.Content = deleteText;
            
            editButton.Click += (s, e) => EditBoundaryBox(box);
            deleteButton.Click += (s, e) => DeleteBoundaryBox(box);
            
            buttonContainer.Children.Add(editButton);
            buttonContainer.Children.Add(deleteButton);
            
            grid.Children.Add(checkBox);
            grid.Children.Add(nameText);
            grid.Children.Add(boundsText);
            grid.Children.Add(buttonContainer);
            
            // Return the grid directly - it's already a Panel
            return grid;
        }
        
        private void AddBoundaryBoxButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get virtual desktop bounds to cover all monitors
                var allScreens = System.Windows.Forms.Screen.AllScreens;
                System.Drawing.Rectangle virtualBounds;
                
                if (allScreens.Length == 0)
                {
                    var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                    virtualBounds = primaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
                }
                else
                {
                    int minX = int.MaxValue, minY = int.MaxValue;
                    int maxX = int.MinValue, maxY = int.MinValue;
                    
                    foreach (var screen in allScreens)
                    {
                        if (screen != null)
                        {
                            minX = Math.Min(minX, screen.Bounds.X);
                            minY = Math.Min(minY, screen.Bounds.Y);
                            maxX = Math.Max(maxX, screen.Bounds.X + screen.Bounds.Width);
                            maxY = Math.Max(maxY, screen.Bounds.Y + screen.Bounds.Height);
                        }
                    }
                    
                    virtualBounds = new System.Drawing.Rectangle(minX, minY, maxX - minX, maxY - minY);
                }
                
                // Don't hide the settings window - just show the boundary window on top
                // Hiding and showing breaks the dialog state when SettingsWindow is shown as ShowDialog()
                try
                {
                    // Create boundary selection window covering all monitors
                    var boundaryWindow = new BoundarySelectionWindow(virtualBounds, "All Monitors");
                    boundaryWindow.Owner = this; // Set owner so it appears on top
                    bool? result = boundaryWindow.ShowDialog();
                    
                    // Check result or SelectedBoundary (in case DialogResult wasn't set)
                    if ((result == true || boundaryWindow.SelectedBoundary.HasValue) && boundaryWindow.SelectedBoundary.HasValue)
                    {
                        // Create new boundary box (independent entity, not tied to a monitor)
                        if (_originalSettings.MagnifierBoundaryBoxes == null)
                        {
                            _originalSettings.MagnifierBoundaryBoxes = new List<Models.MagnifierBoundaryBox>();
                        }
                        
                        var newBox = new Models.MagnifierBoundaryBox
                        {
                            Name = $"Boundary Box {_originalSettings.MagnifierBoundaryBoxes.Count + 1}",
                            MonitorId = "", // Empty - boundary boxes are independent entities
                            Bounds = boundaryWindow.SelectedBoundary.Value,
                            Enabled = true
                        };
                        
                        _originalSettings.MagnifierBoundaryBoxes.Add(newBox);
                        PopulateMagnifierBoundaryBoxList();
                        // Update theme colors to apply current theme to newly created buttons
                        UpdateThemeColors();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create boundary box: {ex.Message}");
                    MessageBox.Show($"Failed to create boundary box: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to add boundary box: {ex.Message}");
                MessageBox.Show($"Failed to add boundary box: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void EditBoundaryBox(Models.MagnifierBoundaryBox box)
        {
            try
            {
                // Get virtual desktop bounds to cover all monitors
                var allScreens = System.Windows.Forms.Screen.AllScreens;
                System.Drawing.Rectangle virtualBounds;
                
                if (allScreens.Length == 0)
                {
                    var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                    virtualBounds = primaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
                }
                else
                {
                    int minX = int.MaxValue, minY = int.MaxValue;
                    int maxX = int.MinValue, maxY = int.MinValue;
                    
                    foreach (var screen in allScreens)
                    {
                        if (screen != null)
                        {
                            minX = Math.Min(minX, screen.Bounds.X);
                            minY = Math.Min(minY, screen.Bounds.Y);
                            maxX = Math.Max(maxX, screen.Bounds.X + screen.Bounds.Width);
                            maxY = Math.Max(maxY, screen.Bounds.Y + screen.Bounds.Height);
                        }
                    }
                    
                    virtualBounds = new System.Drawing.Rectangle(minX, minY, maxX - minX, maxY - minY);
                }
                
                // Don't hide the settings window - just show the boundary window on top
                // Hiding and showing breaks the dialog state when SettingsWindow is shown as ShowDialog()
                try
                {
                    // Create boundary selection window covering all monitors
                    var boundaryWindow = new BoundarySelectionWindow(virtualBounds, "All Monitors");
                    boundaryWindow.Owner = this; // Set owner so it appears on top
                    bool? result = boundaryWindow.ShowDialog();
                    
                    // Check result or SelectedBoundary (in case DialogResult wasn't set)
                    if ((result == true || boundaryWindow.SelectedBoundary.HasValue) && boundaryWindow.SelectedBoundary.HasValue)
                    {
                        var selectedBounds = boundaryWindow.SelectedBoundary.Value;
                        box.Bounds = selectedBounds;
                        // Keep MonitorId empty - boundary boxes are independent entities
                        
                        PopulateMagnifierBoundaryBoxList();
                        // Update theme colors to apply current theme to newly created buttons
                        UpdateThemeColors();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to edit boundary box: {ex.Message}");
                    MessageBox.Show($"Failed to edit boundary box: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to edit boundary box: {ex.Message}");
                MessageBox.Show($"Failed to edit boundary box: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void DeleteBoundaryBox(Models.MagnifierBoundaryBox box)
        {
            if (_originalSettings.MagnifierBoundaryBoxes != null)
            {
                _originalSettings.MagnifierBoundaryBoxes.Remove(box);
                PopulateMagnifierBoundaryBoxList();
                // Update theme colors to apply current theme to newly created buttons
                UpdateThemeColors();
            }
        }
        
        private void PopulateEditorDisplayMonitorDropdown()
        {
            // Clear existing items
            EditorDisplayMonitorComboBox.Items.Clear();
            
            // Get all screens
            var screens = System.Windows.Forms.Screen.AllScreens;
            
            // Add primary monitor option first
            EditorDisplayMonitorComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Primary Monitor" });
            
            // Add individual monitors
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                var isPrimary = screen.Primary;
                var monitorName = isPrimary ? $"Monitor {i + 1} (Primary)" : $"Monitor {i + 1}";
                EditorDisplayMonitorComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = monitorName });
            }
            
            // Select the saved setting
            foreach (var item in EditorDisplayMonitorComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && 
                    comboItem.Content.ToString() == _originalSettings.ScreenshotEditorDisplayMonitor)
                {
                    EditorDisplayMonitorComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // If no match found, default to "Primary Monitor"
            if (EditorDisplayMonitorComboBox.SelectedItem == null)
            {
                EditorDisplayMonitorComboBox.SelectedIndex = 0;
            }
        }

        private void UpdateOpacityLabels()
        {
            HoverOpacityValue.Text = $"{(HoverOpacitySlider.Value * 100):F1}%";
            DropShadowOpacityValue.Text = $"{(DropShadowOpacitySlider.Value * 100):F1}%";
        }
        
        private Style CreateCloseButtonStyle(System.Windows.Media.Color themeColor)
        {
            var style = new Style(typeof(Button));
            
            // Base properties
            style.Setters.Add(new Setter(Button.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(themeColor)));
            style.Setters.Add(new Setter(Button.WidthProperty, 30.0));
            style.Setters.Add(new Setter(Button.HeightProperty, 30.0));
            style.Setters.Add(new Setter(Button.FontSizeProperty, 16.0));
            style.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
            
            // Template
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            border.AppendChild(contentPresenter);
            template.VisualTree = border;
            
            // Hover trigger with opacity from settings
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            var hoverOpacity = _originalSettings.HoverOpacity;
            var hoverAlpha = (byte)(hoverOpacity * 255);
            var hoverColor = System.Windows.Media.Color.FromArgb(hoverAlpha, themeColor.R, themeColor.G, themeColor.B);
            hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(hoverColor)));
            
            var dropShadow = new DropShadowEffect();
            dropShadow.Color = themeColor;
            dropShadow.BlurRadius = 8;
            dropShadow.ShadowDepth = 0;
            dropShadow.Opacity = _originalSettings.DropShadowOpacity;
            hoverTrigger.Setters.Add(new Setter(Button.EffectProperty, dropShadow));
            
            // Pressed trigger
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            var pressedColor = System.Windows.Media.Color.FromArgb(48, themeColor.R, themeColor.G, themeColor.B); // 48 = 0x30
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(pressedColor)));
            
            template.Triggers.Add(hoverTrigger);
            template.Triggers.Add(pressedTrigger);
            
            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            
            return style;
        }

        private Style CreateModernButtonStyle(System.Windows.Media.Color themeColor, double width = 100.0, double height = 36.0, bool allowDynamicWidth = false)
        {
            var style = new Style(typeof(Button));
            
            // Base properties similar to ModernButtonStyle but with dynamic colors
            style.Setters.Add(new Setter(Button.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
            style.Setters.Add(new Setter(Button.BorderBrushProperty, new SolidColorBrush(themeColor)));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1.5)));
            style.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(16, 10, 16, 10)));
            style.Setters.Add(new Setter(Button.MarginProperty, new Thickness(4)));
            style.Setters.Add(new Setter(Button.FontSizeProperty, 14.0));
            style.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
            
            if (allowDynamicWidth)
            {
                // For dynamic buttons, set MinWidth and MaxWidth instead of fixed Width
                // Don't set Width - let it be determined by content and constraints
                style.Setters.Add(new Setter(Button.MinWidthProperty, width * 0.8));
                style.Setters.Add(new Setter(Button.MaxWidthProperty, width * 1.3));
            }
            else
            {
                style.Setters.Add(new Setter(Button.WidthProperty, width));
            }
            style.Setters.Add(new Setter(Button.HeightProperty, height));
            
            // Template
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            border.AppendChild(contentPresenter);
            template.VisualTree = border;
            
            // Hover trigger with dynamic theme color and opacity from settings
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            var hoverOpacity = _originalSettings.HoverOpacity;
            var hoverAlpha = (byte)(hoverOpacity * 255);
            var hoverColor = System.Windows.Media.Color.FromArgb(hoverAlpha, themeColor.R, themeColor.G, themeColor.B);
            hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(hoverColor)));
            
            var dropShadow = new DropShadowEffect();
            dropShadow.Color = themeColor;
            dropShadow.BlurRadius = 12;
            dropShadow.ShadowDepth = 0;
            dropShadow.Opacity = _originalSettings.DropShadowOpacity;
            hoverTrigger.Setters.Add(new Setter(Button.EffectProperty, dropShadow));
            
            // Pressed trigger
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            var pressedColor = System.Windows.Media.Color.FromArgb(48, themeColor.R, themeColor.G, themeColor.B); // 48 = 0x30
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(pressedColor)));
            pressedTrigger.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(2)));
            
            template.Triggers.Add(hoverTrigger);
            template.Triggers.Add(pressedTrigger);
            
            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            
            return style;
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
                
                // Update Cancel and Save buttons with dynamic hover effects
                if (CancelButton != null)
                {
                    CancelButton.Style = CreateModernButtonStyle(color, 100.0, 36.0);
                    if (CancelButton.Content is TextBlock cancelText)
                        cancelText.Foreground = brush;
                }
                if (SaveButton != null)
                {
                    SaveButton.Style = CreateModernButtonStyle(color, 100.0, 36.0);
                    if (SaveButton.Content is TextBlock saveText)
                        saveText.Foreground = brush;
                }
                
                // Update Browse button with dynamic hover effects
                var browseButton = this.FindName("BrowseButton") as Button;
                if (browseButton != null)
                {
                    browseButton.Style = CreateModernButtonStyle(color, 80.0, 32.0);
                    if (browseButton.Content is TextBlock browseText)
                        browseText.Foreground = brush;
                }
                
                // Update Add Boundary Box button with dynamic hover effects
                if (AddBoundaryBoxButton != null)
                {
                    AddBoundaryBoxButton.Style = CreateModernButtonStyle(color, 150.0, 32.0);
                    if (AddBoundaryBoxButton.Content is TextBlock addBoundaryText)
                        addBoundaryText.Foreground = brush;
                }
                
                // Update slider colors to match theme
                UpdateSliderColors(color);
                
                // Update Edit and Delete buttons in boundary box list
                if (MagnifierBoundaryBoxList != null)
                {
                    foreach (var child in MagnifierBoundaryBoxList.Children)
                    {
                        // Handle both Grid (new) and StackPanel (old) layouts
                        if (child is Panel panel)
                        {
                            // Search for buttons in the panel and its children
                            FindAndUpdateButtons(panel, color, brush);
                        }
                    }
                }
                
                // Update CloseSettingsButton (X button) with dynamic hover effects
                if (CloseSettingsButton != null)
                {
                    CloseSettingsButton.Foreground = brush;
                    // Apply dynamic style that changes with theme
                    CloseSettingsButton.Style = CreateCloseButtonStyle(color);
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
                
                // Update ScrollBarTrackHoverBrush resource for track hover areas
                var scrollBarTrackHoverBrush = this.Resources["ScrollBarTrackHoverBrush"] as System.Windows.Media.SolidColorBrush;
                if (scrollBarTrackHoverBrush != null)
                {
                    // Create a more visible version of the theme color for track hover
                    // Use a higher alpha value (64) to make it more visible against the dark background
                    var trackHoverColor = System.Windows.Media.Color.FromArgb(64, color.R, color.G, color.B); // 64 = 0x40
                    scrollBarTrackHoverBrush.Color = trackHoverColor;
                }
                
                // Update ScrollBarButtonHoverBrush resource for button hover areas
                var scrollBarButtonHoverBrush = this.Resources["ScrollBarButtonHoverBrush"] as System.Windows.Media.SolidColorBrush;
                if (scrollBarButtonHoverBrush != null)
                {
                    // Create a semi-transparent version of the theme color for button hover
                    // Use the same alpha value (21) as the X button hover for consistency
                    var buttonHoverColor = System.Windows.Media.Color.FromArgb(21, color.R, color.G, color.B); // 21 = 0x15
                    scrollBarButtonHoverBrush.Color = buttonHoverColor;
                }
                
                // Force the scrollbar to refresh by invalidating the visual tree
                this.InvalidateVisual();
                

                

                

                

                
                System.Diagnostics.Debug.WriteLine($"Updated settings theme colors: {iconColor}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update theme colors: {ex.Message}");
            }
        }

        private void ApplyOpacityChanges()
        {
            try
            {
                // Temporarily update the settings service so MainWindow can read the new values
                _settingsService.CurrentSettings.HoverOpacity = _originalSettings.HoverOpacity;
                _settingsService.CurrentSettings.DropShadowOpacity = _originalSettings.DropShadowOpacity;
                
                // Update buttons in this settings window with new opacity values
                UpdateSettingsWindowButtonStyles();
                
                // Get the main window to apply opacity changes
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    // Apply the opacity changes immediately
                    mainWindow.ApplyThemeSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply opacity changes: {ex.Message}");
            }
        }
        
        private void UpdateSettingsWindowButtonStyles()
        {
            try
            {
                var iconColor = _originalSettings.IconColor;
                if (string.IsNullOrEmpty(iconColor))
                    iconColor = "#FFFF8C00"; // Default orange
                
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(iconColor);
                var brush = new System.Windows.Media.SolidColorBrush(color);
                
                // Update Cancel and Save buttons
                if (CancelButton != null)
                {
                    CancelButton.Style = CreateModernButtonStyle(color, 100.0, 36.0);
                    if (CancelButton.Content is TextBlock cancelText)
                        cancelText.Foreground = brush;
                }
                if (SaveButton != null)
                {
                    SaveButton.Style = CreateModernButtonStyle(color, 100.0, 36.0);
                    if (SaveButton.Content is TextBlock saveText)
                        saveText.Foreground = brush;
                }
                
                // Update Browse button
                var browseButton = this.FindName("BrowseButton") as Button;
                if (browseButton != null)
                {
                    browseButton.Style = CreateModernButtonStyle(color, 80.0, 32.0);
                    if (browseButton.Content is TextBlock browseText)
                        browseText.Foreground = brush;
                }
                
                // Update Close button
                if (CloseSettingsButton != null)
                {
                    CloseSettingsButton.Style = CreateCloseButtonStyle(color);
                    CloseSettingsButton.Foreground = brush;
                }
                
                // Update Add Boundary Box button
                if (AddBoundaryBoxButton != null)
                {
                    AddBoundaryBoxButton.Style = CreateModernButtonStyle(color, 150.0, 32.0);
                    if (AddBoundaryBoxButton.Content is TextBlock addBoundaryText)
                        addBoundaryText.Foreground = brush;
                }
                
                // Update Edit and Delete buttons in boundary box list
                if (MagnifierBoundaryBoxList != null)
                {
                    foreach (var child in MagnifierBoundaryBoxList.Children)
                    {
                        if (child is Panel panel)
                        {
                            FindAndUpdateButtons(panel, color, brush);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update settings window button styles: {ex.Message}");
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
                    IconColorWheel.ColorChanged -= (s, color) => 
                    {
                        IconColorTextBox.Text = color;
                        UpdateThemeColors();
                    };
                    
                    // Update the color wheel
                    IconColorWheel.SetColor(text);
                    
                    // Re-add the event handler
                    IconColorWheel.ColorChanged += (s, color) => 
                    {
                        IconColorTextBox.Text = color;
                        UpdateThemeColors();
                    };
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
                LogToFile("=== Loading Audio Devices (Auto) ===");
                
                // Clear existing devices
                OutputAudioDeviceComboBox.Items.Clear();
                InputAudioDeviceComboBox.Items.Clear();
                
                // Add "Auto-detect" options
                OutputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Auto-detect" });
                InputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Auto-detect" });
                
                // Get available devices (prioritizes ScreenRecorderLib)
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
                    else if (IsOutputDevice(device))
                    {
                        outputDevices.Add(device);
                        LogToFile($"Added to output devices: {device}");
                    }
                    else
                    {
                        // If we can't determine, add to both but log it
                        inputDevices.Add(device);
                        outputDevices.Add(device);
                        LogToFile($"Added to both input and output (undetermined): {device}");
                    }
                }
                
                LogToFile($"Categorized {inputDevices.Count} input devices and {outputDevices.Count} output devices");
                
                // Populate dropdowns
                foreach (var device in inputDevices)
                {
                    InputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = device });
                }
                
                foreach (var device in outputDevices)
                {
                    OutputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = device });
                }
                
                LogToFile("Audio device loading completed successfully");
            }
            catch (Exception ex)
            {
                LogToFile($"Error loading audio devices: {ex.Message}");
                // On error, just leave with Auto-detect option
                OutputAudioDeviceComboBox.Items.Clear();
                OutputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Auto-detect" });
                
                InputAudioDeviceComboBox.Items.Clear();
                InputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Auto-detect" });
            }
        }

        private void SetSelectedAudioDevices()
        {
            LogToFile($"Setting selected audio devices...");
            LogToFile($"Saved output device: '{_originalSettings.SelectedOutputAudioDevice}'");
            LogToFile($"Saved input device: '{_originalSettings.SelectedInputAudioDevice}'");
            
            // Set output audio device
            bool outputDeviceSet = false;
            foreach (var item in OutputAudioDeviceComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem)
                {
                    var deviceName = comboItem.Content.ToString();
                    LogToFile($"Checking output device: '{deviceName}'");
                    
                    if (deviceName == _originalSettings.SelectedOutputAudioDevice)
                    {
                        OutputAudioDeviceComboBox.SelectedItem = item;
                        LogToFile($"Set output device to: {deviceName}");
                        outputDeviceSet = true;
                        break;
                    }
                }
            }
            
            if (!outputDeviceSet)
            {
                LogToFile($"Could not find saved output device '{_originalSettings.SelectedOutputAudioDevice}' in dropdown");
                // Set to "Auto-detect" if device not found
                if (OutputAudioDeviceComboBox.Items.Count > 0)
                {
                    OutputAudioDeviceComboBox.SelectedIndex = 0;
                    LogToFile("Set output device to Auto-detect (default)");
                }
            }

            // Set input audio device
            bool inputDeviceSet = false;
            foreach (var item in InputAudioDeviceComboBox.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem)
                {
                    var deviceName = comboItem.Content.ToString();
                    LogToFile($"Checking input device: '{deviceName}'");
                    
                    if (deviceName == _originalSettings.SelectedInputAudioDevice)
                    {
                        InputAudioDeviceComboBox.SelectedItem = item;
                        LogToFile($"Set input device to: {deviceName}");
                        inputDeviceSet = true;
                        break;
                    }
                }
            }
            
            if (!inputDeviceSet)
            {
                LogToFile($"Could not find saved input device '{_originalSettings.SelectedInputAudioDevice}' in dropdown");
                // Set to "Auto-detect" if device not found
                if (InputAudioDeviceComboBox.Items.Count > 0)
                {
                    InputAudioDeviceComboBox.SelectedIndex = 0;
                    LogToFile("Set input device to Auto-detect (default)");
                }
            }
        }

        private bool IsInputDevice(string deviceName)
        {
            var deviceLower = deviceName.ToLower();
            
            // Strong input device indicators (high confidence)
            var strongInputKeywords = new[] { 
                "microphone", "mic", "input", "capture", "line in", "aux in", "stereo mix",
                "what u hear", "cable input", "vb-audio", "system audio", "loopback"
            };
            
            // Check for strong input indicators
            foreach (var keyword in strongInputKeywords)
            {
                if (deviceLower.Contains(keyword))
                {
                    LogToFile($"Device '{deviceName}' classified as INPUT (strong indicator: {keyword})");
                    return true;
                }
            }
            
            // Check for output device indicators that would make this NOT an input
            var outputKeywords = new[] { 
                "speakers", "headphones", "earphones", "monitor", "display", "output",
                "audio out", "playback", "render", "sink"
            };
            foreach (var keyword in outputKeywords)
            {
                if (deviceLower.Contains(keyword))
                {
                    LogToFile($"Device '{deviceName}' classified as NOT INPUT (output indicator: {keyword})");
                    return false; // This is likely an output device
                }
            }
            
            // Check for ambiguous terms that could be either
            var ambiguousKeywords = new[] { "audio", "sound", "realtek", "intel", "amd", "nvidia" };
            foreach (var keyword in ambiguousKeywords)
            {
                if (deviceLower.Contains(keyword))
                {
                    LogToFile($"Device '{deviceName}' has ambiguous keyword '{keyword}' - defaulting to NOT INPUT");
                    return false; // Default to not input for ambiguous terms
                }
            }
            
            LogToFile($"Device '{deviceName}' classified as NOT INPUT (no clear indicators)");
            return false;
        }

        private bool IsOutputDevice(string deviceName)
        {
            var deviceLower = deviceName.ToLower();
            
            // Strong output device indicators (high confidence)
            var strongOutputKeywords = new[] { 
                "speakers", "headphones", "earphones", "monitor", "display", "output",
                "audio out", "playback", "render", "sink", "stereo", "surround"
            };
            
            // Check for strong output indicators
            foreach (var keyword in strongOutputKeywords)
            {
                if (deviceLower.Contains(keyword))
                {
                    LogToFile($"Device '{deviceName}' classified as OUTPUT (strong indicator: {keyword})");
                    return true;
                }
            }
            
            // Check for input device indicators that would make this NOT an output
            var inputKeywords = new[] { 
                "microphone", "mic", "input", "capture", "line in", "aux in",
                "what u hear", "cable input", "vb-audio", "system audio", "loopback"
            };
            foreach (var keyword in inputKeywords)
            {
                if (deviceLower.Contains(keyword))
                {
                    LogToFile($"Device '{deviceName}' classified as NOT OUTPUT (input indicator: {keyword})");
                    return false; // This is likely an input device
                }
            }
            
            // Check for ambiguous terms that could be either
            var ambiguousKeywords = new[] { "audio", "sound", "realtek", "intel", "amd", "nvidia" };
            foreach (var keyword in ambiguousKeywords)
            {
                if (deviceLower.Contains(keyword))
                {
                    LogToFile($"Device '{deviceName}' has ambiguous keyword '{keyword}' - defaulting to NOT OUTPUT");
                    return false; // Default to not output for ambiguous terms
                }
            }
            
            LogToFile($"Device '{deviceName}' classified as NOT OUTPUT (no clear indicators)");
            return false;
        }





        private List<string> GetAvailableAudioDevices()
        {
            var devices = new List<string>();
            
            try
            {
                LogToFile("=== Getting available audio devices ===");
                
                // Try multiple approaches to get audio devices
                var allDevices = new List<string>();
                
                // Method 1: Try using ScreenRecorderLib's built-in device enumeration
                // var screenRecorderDevices = GetScreenRecorderLibDevices();
                // if (screenRecorderDevices.Count > 0)
                // {
                //     LogToFile($"ScreenRecorderLib found {screenRecorderDevices.Count} devices");
                //     allDevices.AddRange(screenRecorderDevices);
                // }
                // else
                // {
                //     LogToFile("ScreenRecorderLib found no devices, trying fallback methods");
                // }
                
                // Method 2: Try using NAudio (if available) - most reliable
                var naudioDevices = GetNAudioDevices();
                if (naudioDevices.Count > 0)
                {
                    LogToFile($"NAudio found {naudioDevices.Count} devices");
                    foreach (var device in naudioDevices)
                    {
                        if (!allDevices.Contains(device))
                        {
                            allDevices.Add(device);
                        }
                    }
                }
                
                // Method 3: Try using WMI - Windows Management Instrumentation
                var wmiDevices = GetWMIAudioDevices();
                if (wmiDevices.Count > 0)
                {
                    LogToFile($"WMI found {wmiDevices.Count} devices");
                    foreach (var device in wmiDevices)
                    {
                        if (!allDevices.Contains(device))
                        {
                            allDevices.Add(device);
                        }
                    }
                }
                
                // Method 4: Try using Windows Core Audio API (simplified)
                var coreAudioDevices = GetSimplifiedCoreAudioDevices();
                if (coreAudioDevices.Count > 0)
                {
                    LogToFile($"Core Audio API found {coreAudioDevices.Count} devices");
                    foreach (var device in coreAudioDevices)
                    {
                        if (!allDevices.Contains(device))
                        {
                            allDevices.Add(device);
                        }
                    }
                }
                
                // Method 5: Try FFmpeg as last resort
                if (allDevices.Count == 0)
                {
                    LogToFile("No devices found with other methods, trying FFmpeg");
                    var ffmpegDevices = GetFFmpegAudioDevices();
                    allDevices.AddRange(ffmpegDevices);
                }
                
                // Remove duplicates and sort
                devices = allDevices.Distinct().OrderBy(d => d).ToList();
                
                LogToFile($"Total unique audio devices found: {devices.Count}");
                return devices;
            }
            catch (Exception ex)
            {
                LogToFile($"Error getting audio devices: {ex.Message}");
                return devices;
            }
        }

        // private List<string> GetScreenRecorderLibDevices()
        // {
        //     var devices = new List<string>();
        //     
        //     try
        //     {
        //         LogToFile("Getting audio devices using ScreenRecorderLib...");
        //         
        //         // ScreenRecorderLib provides access to audio devices through its API
        //         // We can use the library's built-in capabilities to get device information
        //         
        //         // For now, we'll use a simplified approach that leverages ScreenRecorderLib's
        //         // understanding of the audio system, but we'll still use our Windows API
        //         // enumeration since ScreenRecorderLib doesn't expose device enumeration directly
        //         
        //         // Get devices using the real Windows Core Audio API approach
        //         var inputDevices = GetRealAudioDevicesByType("input");
        //         var outputDevices = GetRealAudioDevicesByType("output");
        //         
        //         devices.AddRange(inputDevices);
        //         devices.AddRange(outputDevices);
        //         
        //         LogToFile($"ScreenRecorderLib-compatible devices found: {devices.Count} (Input: {inputDevices.Count}, Output: {outputDevices.Count})");
        //         return devices;
        //     }
        //     catch (Exception ex)
        //     {
        //         LogToFile($"Error getting ScreenRecorderLib audio devices: {ex.Message}");
        //         return devices;
        //     }
        // }

        private List<string> GetRealAudioDevicesByType(string deviceType)
        {
            var devices = new List<string>();
            
            try
            {
                LogToFile($"Getting real {deviceType} devices using Windows Core Audio API...");
                
                // Use Windows Core Audio API to get real device names
                WindowsCoreAudioAPI.CoInitialize(IntPtr.Zero);
                
                try
                {
                    // Create device enumerator
                    var deviceEnumeratorType = typeof(MMDeviceEnumerator).GUID;
                    var deviceEnumeratorInterface = typeof(IMMDeviceEnumerator).GUID;
                    var result = WindowsCoreAudioAPI.CoCreateInstance(
                        ref deviceEnumeratorType, 
                        IntPtr.Zero, 
                        1, // CLSCTX_INPROC_SERVER
                        ref deviceEnumeratorInterface, 
                        out IntPtr deviceEnumeratorPtr);
                    
                    if (result == 0)
                    {
                        var deviceEnumerator = (IMMDeviceEnumerator)Marshal.GetTypedObjectForIUnknown(deviceEnumeratorPtr, typeof(IMMDeviceEnumerator));
                        
                        // Get devices based on type
                        int dataFlow = deviceType == "input" ? WindowsCoreAudioAPI.eCapture : WindowsCoreAudioAPI.eRender;
                        var flowDevices = GetRealAudioDevicesByFlow(deviceEnumerator, dataFlow);
                        devices.AddRange(flowDevices);
                        
                        // Cleanup
                        Marshal.ReleaseComObject(deviceEnumerator);
                    }
                    else
                    {
                        LogToFile($"Failed to create device enumerator for {deviceType}: {result}");
                    }
                }
                finally
                {
                    // Always cleanup COM
                    WindowsCoreAudioAPI.CoUninitialize();
                }
                
                LogToFile($"Real {deviceType} devices found: {devices.Count}");
                return devices;
            }
            catch (Exception ex)
            {
                LogToFile($"Error getting real {deviceType} devices: {ex.Message}");
                return devices;
            }
        }

        private List<string> GetNAudioDevices()
        {
            var devices = new List<string>();
            
            try
            {
                LogToFile("Trying NAudio device enumeration...");
                
                // Try to use NAudio if it's available
                // This is a more reliable approach than the complex Core Audio API
                
                // For now, we'll use a simplified approach that doesn't require NAudio
                // but provides better device enumeration than the current implementation
                
                // Get devices using a simpler Windows API approach
                var inputDevices = GetSimpleAudioDevices("input");
                var outputDevices = GetSimpleAudioDevices("output");
                
                devices.AddRange(inputDevices);
                devices.AddRange(outputDevices);
                
                LogToFile($"Simple enumeration found {devices.Count} devices");
                return devices;
            }
            catch (Exception ex)
            {
                LogToFile($"Error in NAudio device enumeration: {ex.Message}");
                return devices;
            }
        }

        private List<string> GetWMIAudioDevices()
        {
            var devices = new List<string>();
            
            try
            {
                LogToFile("Getting audio devices using WMI...");
                
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SoundDevice"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            var deviceName = obj["Name"]?.ToString();
                            if (!string.IsNullOrEmpty(deviceName))
                            {
                                devices.Add(deviceName);
                                LogToFile($"WMI found device: {deviceName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogToFile($"Error processing WMI device: {ex.Message}");
                        }
                    }
                }
                
                LogToFile($"WMI found {devices.Count} devices");
                return devices;
            }
            catch (Exception ex)
            {
                LogToFile($"Error getting WMI audio devices: {ex.Message}");
                return devices;
            }
        }

        private List<string> GetSimplifiedCoreAudioDevices()
        {
            var devices = new List<string>();
            
            try
            {
                LogToFile("Getting audio devices using simplified Core Audio API...");
                
                // Try to get devices using the existing GetSimpleAudioDevices method
                var inputDevices = GetSimpleAudioDevices("input");
                var outputDevices = GetSimpleAudioDevices("output");
                
                devices.AddRange(inputDevices);
                devices.AddRange(outputDevices);
                
                LogToFile($"Simplified Core Audio API found {devices.Count} devices");
                return devices;
            }
            catch (Exception ex)
            {
                LogToFile($"Error in simplified Core Audio API: {ex.Message}");
                return devices;
            }
        }

        private List<string> GetSimpleAudioDevices(string deviceType)
        {
            var devices = new List<string>();
            
            try
            {
                LogToFile($"Getting real {deviceType} devices using Windows Core Audio API...");
                
                // Use Windows Core Audio API to get real device names
                WindowsCoreAudioAPI.CoInitialize(IntPtr.Zero);
                
                try
                {
                    // Create device enumerator
                    var deviceEnumeratorType = typeof(MMDeviceEnumerator).GUID;
                    var deviceEnumeratorInterface = typeof(IMMDeviceEnumerator).GUID;
                    var result = WindowsCoreAudioAPI.CoCreateInstance(
                        ref deviceEnumeratorType, 
                        IntPtr.Zero, 
                        1, // CLSCTX_INPROC_SERVER
                        ref deviceEnumeratorInterface, 
                        out IntPtr deviceEnumeratorPtr);
                    
                    if (result == 0)
                    {
                        var deviceEnumerator = (IMMDeviceEnumerator)Marshal.GetTypedObjectForIUnknown(deviceEnumeratorPtr, typeof(IMMDeviceEnumerator));
                        
                        // Get devices based on type
                        int dataFlow = deviceType == "input" ? WindowsCoreAudioAPI.eCapture : WindowsCoreAudioAPI.eRender;
                        var flowDevices = GetRealAudioDevicesByFlow(deviceEnumerator, dataFlow);
                        devices.AddRange(flowDevices);
                        
                        // Cleanup
                        Marshal.ReleaseComObject(deviceEnumerator);
                    }
                    else
                    {
                        LogToFile($"Failed to create device enumerator for {deviceType}: {result}");
                    }
                }
                finally
                {
                    // Always cleanup COM
                    WindowsCoreAudioAPI.CoUninitialize();
                }
                
                LogToFile($"Real {deviceType} devices found: {devices.Count}");
                return devices;
            }
            catch (Exception ex)
            {
                LogToFile($"Error getting real {deviceType} devices: {ex.Message}");
                return devices;
            }
        }

        private List<string> GetRealAudioDevicesByFlow(IMMDeviceEnumerator enumerator, int dataFlow)
        {
            var devices = new List<string>();
            
            try
            {
                LogToFile($"Starting real enumeration for data flow {dataFlow} ({(dataFlow == WindowsCoreAudioAPI.eCapture ? "Capture" : "Render")})");
                
                var result = enumerator.EnumAudioEndpoints(dataFlow, WindowsCoreAudioAPI.DEVICE_STATE_ACTIVE, out IntPtr deviceCollectionPtr);
                
                if (result != 0)
                {
                    LogToFile($"Failed to enumerate audio endpoints for flow {dataFlow}: {result}");
                    return devices;
                }
                
                LogToFile($"Successfully enumerated audio endpoints for flow {dataFlow}");
                
                // Get the device collection interface
                var deviceCollection = (IMMDeviceCollection)Marshal.GetTypedObjectForIUnknown(deviceCollectionPtr, typeof(IMMDeviceCollection));
                
                // Get the count of devices
                uint deviceCount;
                deviceCollection.GetCount(out deviceCount);
                LogToFile($"Found {deviceCount} devices for flow {dataFlow}");
                
                // Iterate through each device
                for (uint i = 0; i < deviceCount; i++)
                {
                    IntPtr devicePtr;
                    var getDeviceResult = deviceCollection.Item(i, out devicePtr);
                    
                    if (getDeviceResult == 0 && devicePtr != IntPtr.Zero)
                    {
                        var device = (IMMDevice)Marshal.GetTypedObjectForIUnknown(devicePtr, typeof(IMMDevice));
                        
                        try
                        {
                            // Get the device ID
                            string deviceId;
                            device.GetId(out deviceId);
                            LogToFile($"Processing real device {i}: {deviceId}");
                            
                            // Get the device friendly name
                            IntPtr propertyStorePtr;
                            var openPropertyStoreResult = device.OpenPropertyStore(0x00000000, out propertyStorePtr); // STGM_READ
                            
                            if (openPropertyStoreResult == 0 && propertyStorePtr != IntPtr.Zero)
                            {
                                var propertyStore = (IPropertyStore)Marshal.GetTypedObjectForIUnknown(propertyStorePtr, typeof(IPropertyStore));
                                
                                try
                                {
                                    // Get the friendly name property
                                    var friendlyNameKey = new PROPERTYKEY
                                    {
                                        fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
                                        pid = 14 // PKEY_Device_FriendlyName
                                    };
                                    
                                    PROPVARIANT propVariant;
                                    var getValueResult = propertyStore.GetValue(ref friendlyNameKey, out propVariant);
                                    
                                    if (getValueResult == 0)
                                    {
                                        LogToFile($"Successfully retrieved property for device {deviceId}, variant type: {propVariant.vt}");
                                        
                                        // Extract the string value from PROPVARIANT
                                        if (propVariant.vt == 31) // VT_LPWSTR
                                        {
                                            string? deviceName = Marshal.PtrToStringUni(propVariant.data);
                                            if (!string.IsNullOrEmpty(deviceName) && !devices.Contains(deviceName))
                                            {
                                                devices.Add(deviceName);
                                                LogToFile($"Added real device: {deviceName} (ID: {deviceId})");
                                            }
                                            else if (string.IsNullOrEmpty(deviceName))
                                            {
                                                LogToFile($"Device name is null or empty for device {deviceId}");
                                            }
                                            else
                                            {
                                                LogToFile($"Device {deviceName} already exists in list");
                                            }
                                        }
                                        else
                                        {
                                            LogToFile($"Unexpected variant type {propVariant.vt} for device {deviceId}");
                                        }
                                        
                                        // Clear the PROPVARIANT
                                        var handle = GCHandle.Alloc(propVariant, GCHandleType.Pinned);
                                        try
                                        {
                                            WindowsCoreAudioAPI.VariantClear(handle.AddrOfPinnedObject());
                                        }
                                        finally
                                        {
                                            handle.Free();
                                        }
                                    }
                                    else
                                    {
                                        LogToFile($"Failed to get property value for device {deviceId}: {getValueResult}");
                                    }
                                }
                                finally
                                {
                                    Marshal.ReleaseComObject(propertyStore);
                                }
                            }
                            else
                            {
                                LogToFile($"Failed to open property store for device {deviceId}: {openPropertyStoreResult}");
                            }
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(device);
                        }
                    }
                    else
                    {
                        LogToFile($"Failed to get device {i}: {getDeviceResult}");
                    }
                }
                
                Marshal.ReleaseComObject(deviceCollection);
                
                LogToFile($"Successfully enumerated {devices.Count} real devices for flow {dataFlow}");
                return devices;
            }
            catch (Exception ex)
            {
                LogToFile($"Error getting real audio devices by flow: {ex.Message}");
                return devices;
            }
        }

        private List<string> GetFFmpegAudioDevices()
        {
            var devices = new List<string>();
            
            try
            {
                LogToFile("Getting FFmpeg audio devices...");
                
                var ffmpegPath = GetFFmpegPath();
                if (string.IsNullOrEmpty(ffmpegPath))
                {
                    LogToFile("FFmpeg not found, skipping FFmpeg device detection");
                    return devices;
                }
                
                // Try both DirectShow and WASAPI to get comprehensive device list
                var dshowDevices = GetFFmpegDevicesByFormat(ffmpegPath, "dshow");
                var wasapiDevices = GetFFmpegDevicesByFormat(ffmpegPath, "wasapi");
                
                // Combine devices, avoiding duplicates
                foreach (var device in dshowDevices)
                {
                    if (!devices.Contains(device))
                    {
                        devices.Add(device);
                    }
                }
                
                foreach (var device in wasapiDevices)
                {
                    if (!devices.Contains(device))
                    {
                        devices.Add(device);
                    }
                }
                
                LogToFile($"FFmpeg found {devices.Count} total devices (DirectShow: {dshowDevices.Count}, WASAPI: {wasapiDevices.Count})");
                return devices;
            }
            catch (Exception ex)
            {
                LogToFile($"Error getting FFmpeg audio devices: {ex.Message}");
                return devices;
            }
        }

        private List<string> GetFFmpegDevicesByFormat(string ffmpegPath, string format)
        {
            var devices = new List<string>();
            
            try
            {
                LogToFile($"Getting FFmpeg devices using {format} format...");
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-list_devices true -f {format} -i dummy",
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
                
                LogToFile($"FFmpeg {format} exit code: {process.ExitCode}");
                LogToFile($"FFmpeg {format} stderr length: {output.Length}");
                LogToFile($"FFmpeg {format} stdout length: {stdout.Length}");
                
                // Parse FFmpeg output for devices
                var lines = output.Split('\n');
                bool inAudioDevices = false;
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Look for audio device sections
                    if (trimmedLine.Contains("DirectShow audio devices") || trimmedLine.Contains("WASAPI audio devices"))
                    {
                        inAudioDevices = true;
                        LogToFile($"Found audio devices section: {trimmedLine}");
                        continue;
                    }
                    
                    // Skip video device sections
                    if (trimmedLine.Contains("DirectShow video devices") || trimmedLine.Contains("WASAPI video devices"))
                    {
                        inAudioDevices = false;
                        continue;
                    }
                    
                    // Parse device names (they're usually in quotes)
                    if (inAudioDevices && trimmedLine.Contains("\""))
                    {
                        var startIndex = trimmedLine.IndexOf('"') + 1;
                        var endIndex = trimmedLine.LastIndexOf('"');
                        if (startIndex > 0 && endIndex > startIndex)
                        {
                            var deviceName = trimmedLine.Substring(startIndex, endIndex - startIndex);
                            if (!string.IsNullOrEmpty(deviceName) && !devices.Contains(deviceName))
                            {
                                devices.Add(deviceName);
                                LogToFile($"Added {format} device: {deviceName}");
                            }
                        }
                    }
                }
                
                LogToFile($"FFmpeg {format} found {devices.Count} devices");
                return devices;
            }
            catch (Exception ex)
            {
                LogToFile($"Error getting FFmpeg {format} devices: {ex.Message}");
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

        // Audio recording mode combo box event handler commented out since combo box is disabled
        /*
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
        */

        private void RefreshAudioDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToFile("=== Refreshing Audio Devices ===");
                
                // Test new device enumeration methods
                // var screenRecorderDevices = GetScreenRecorderLibDevices();
                var naudioDevices = GetNAudioDevices();
                var wmiDevices = GetWMIAudioDevices();
                var coreAudioDevices = GetSimplifiedCoreAudioDevices();
                
                // Test FFmpeg as fallback
                var ffmpegPath = GetFFmpegPath();
                var ffmpegFound = !string.IsNullOrEmpty(ffmpegPath);
                var testResult = TestFFmpegAudioDevices();
                
                // Clear existing devices
                OutputAudioDeviceComboBox.Items.Clear();
                InputAudioDeviceComboBox.Items.Clear();
                
                // Add "Auto-detect" options
                OutputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Auto-detect" });
                InputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Auto-detect" });
                
                // Get available devices (prioritizes ScreenRecorderLib)
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
                    else if (IsOutputDevice(device))
                    {
                        outputDevices.Add(device);
                        LogToFile($"Added to output devices: {device}");
                    }
                    else
                    {
                        // If we can't determine, add to both but log it
                        inputDevices.Add(device);
                        outputDevices.Add(device);
                        LogToFile($"Added to both input and output (undetermined): {device}");
                    }
                }
                
                LogToFile($"Categorized {inputDevices.Count} input devices and {outputDevices.Count} output devices");
                
                // Populate dropdowns
                foreach (var device in inputDevices)
                {
                    InputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = device });
                }
                
                foreach (var device in outputDevices)
                {
                    OutputAudioDeviceComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = device });
                }
                
                // Set selected devices
                SetSelectedAudioDevices();
                
                var message = $"=== Audio Device Detection Results ===\n\n";
                message += $"Enumeration Methods:\n";
                // message += $"  - ScreenRecorderLib devices: {screenRecorderDevices.Count}\n";
                message += $"  - NAudio devices: {naudioDevices.Count}\n";
                message += $"  - WMI devices: {wmiDevices.Count}\n";
                message += $"  - Core Audio devices: {coreAudioDevices.Count}\n\n";
                
                if (ffmpegFound)
                {
                    message += $"FFmpeg (Fallback):\n";
                    message += $"  - Found: {ffmpegFound}\n";
                    message += $"  - Path: {ffmpegPath}\n";
                    message += $"  - Test result: {testResult}\n\n";
                }
                
                message += $"Final Results:\n";
                message += $"  - Input devices: {inputDevices.Count}\n";
                message += $"  - Output devices: {outputDevices.Count}\n\n";
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
                
                var result = new System.Text.StringBuilder();
                result.AppendLine($"FFmpeg path: {ffmpegPath}");
                
                // Test both DirectShow and WASAPI
                var dshowDevices = GetFFmpegDevicesByFormat(ffmpegPath, "dshow");
                var wasapiDevices = GetFFmpegDevicesByFormat(ffmpegPath, "wasapi");
                
                result.AppendLine($"DirectShow devices found: {dshowDevices.Count}");
                foreach (var device in dshowDevices)
                {
                    result.AppendLine($"  - {device}");
                }
                
                result.AppendLine($"WASAPI devices found: {wasapiDevices.Count}");
                foreach (var device in wasapiDevices)
                {
                    result.AppendLine($"  - {device}");
                }
                
                var totalDevices = dshowDevices.Count + wasapiDevices.Count;
                result.AppendLine($"Total unique devices: {totalDevices}");
                
                return result.ToString();
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

        private async void ShowOBSSettings()
        {
            try
            {
                LogToFile("OBS recording engine selected - setting up OBS automatically");
                
                // Show immediate feedback
                MessageBox.Show(
                    "Setting up OBS Studio...\n\n" +
                    "This may take a few moments. Please wait.",
                    "OBS Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                // Use the new OBS service to handle setup
                var obsService = new SharpShot.Services.OBSRecordingService(_settingsService);
                var success = await obsService.SetupOBSForRecordingAsync();
                
                if (success)
                {
                    MessageBox.Show(
                        "OBS Studio has been automatically configured and is ready for recording!\n\n" +
                        "• OBS Studio is installed and running\n" +
                        "• Auto-configuration completed\n\n" +
                        "You can now use OBS recording engine for enhanced audio recording.",
                        "OBS Setup Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Check if OBS is actually available before showing the warning
                    var obsPath = SharpShot.Utils.OBSDetection.FindOBSPath();
                    if (!string.IsNullOrEmpty(obsPath))
                    {
                        MessageBox.Show(
                            "OBS Studio is available but setup encountered a minor issue.\n\n" +
                            "Recording will work normally - the setup will complete automatically when you start recording.",
                            "OBS Setup Note",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            "OBS Studio setup encountered an issue.\n\n" +
                            "Please ensure OBS Studio is available in the application directory.\n" +
                            "Recording will work in fallback mode.",
                            "OBS Setup Issue",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error setting up OBS: {ex.Message}");
                
                // Check if OBS is available despite the error
                var obsPath = SharpShot.Utils.OBSDetection.FindOBSPath();
                if (!string.IsNullOrEmpty(obsPath))
                {
                    MessageBox.Show(
                        "OBS Studio is available but encountered a setup error.\n\n" +
                        "Recording will work normally - the setup will complete automatically when you start recording.",
                        "OBS Setup Note",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Error setting up OBS Studio: {ex.Message}\n\n" +
                        "Recording will work in fallback mode.",
                        "OBS Setup Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private async Task InstallOBSAsync()
        {
            try
            {
                LogToFile("Starting automatic OBS installation...");
                
                var bundlingService = new SharpShot.Services.OBSBundlingService();
                var success = await bundlingService.InstallOBSAsync();
                
                if (success)
                {
                    MessageBox.Show(
                        "OBS Studio has been successfully installed and configured!\n\n" +
                        "You can now use OBS recording engine for enhanced audio recording.",
                        "Installation Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Failed to install OBS Studio automatically.\n\n" +
                        "Please install OBS Studio manually from https://obsproject.com/",
                        "Installation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error during OBS installation: {ex.Message}");
                MessageBox.Show(
                    $"Error installing OBS Studio: {ex.Message}\n\n" +
                    "Please install OBS Studio manually from https://obsproject.com/",
                    "Installation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void HideOBSSettings()
        {
            LogToFile("OBS recording engine deselected - hiding OBS-specific settings");
        }
    }
}