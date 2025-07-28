using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpShot.Services;

namespace SharpShot.Utils
{
    public class HotkeyManager : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly Dictionary<int, Action> _registeredHotkeys;
        private readonly Dictionary<string, int> _hotkeyIds;
        private int _nextHotkeyId = 1;
        private bool _isInitialized;
        private IntPtr _windowHandle;

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        public HotkeyManager(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _registeredHotkeys = new Dictionary<int, Action>();
            _hotkeyIds = new Dictionary<string, int>();
            _isInitialized = false;
        }
        
        public void SetWindowHandle(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            _isInitialized = true;

            if (_settingsService.CurrentSettings.EnableGlobalHotkeys)
            {
                RegisterHotkeys();
            }
        }

        public void RegisterHotkeys()
        {
            if (!_isInitialized) return;

            // Unregister existing hotkeys
            UnregisterAllHotkeys();

            // Register hotkeys from settings
            RegisterHotkey("RegionCapture", _settingsService.CurrentSettings.Hotkeys.GetValueOrDefault("RegionCapture", "Double Ctrl"));
            RegisterHotkey("FullScreenCapture", _settingsService.CurrentSettings.Hotkeys.GetValueOrDefault("FullScreenCapture", "Ctrl+Shift+S"));
            RegisterHotkey("ToggleRecording", _settingsService.CurrentSettings.Hotkeys.GetValueOrDefault("ToggleRecording", "Ctrl+Shift+R"));
            RegisterHotkey("Cancel", _settingsService.CurrentSettings.Hotkeys.GetValueOrDefault("Cancel", "Escape"));
            RegisterHotkey("Save", _settingsService.CurrentSettings.Hotkeys.GetValueOrDefault("Save", "Space"));
            RegisterHotkey("Copy", _settingsService.CurrentSettings.Hotkeys.GetValueOrDefault("Copy", "Enter"));
        }

        private void RegisterHotkey(string actionName, string hotkeyString)
        {
            try
            {
                var (modifiers, keyCode) = ParseHotkey(hotkeyString);
                if (keyCode == 0) return; // Invalid hotkey

                var hotkeyId = _nextHotkeyId++;
                if (_windowHandle != IntPtr.Zero)
                {
                    if (RegisterHotKey(_windowHandle, hotkeyId, modifiers, keyCode))
                    {
                        _registeredHotkeys[hotkeyId] = GetActionForHotkey(actionName);
                        _hotkeyIds[actionName] = hotkeyId;
                        System.Diagnostics.Debug.WriteLine($"Registered hotkey: {actionName} = {hotkeyString}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to register hotkey: {actionName} = {hotkeyString}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Window handle not set, cannot register hotkey: {actionName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registering hotkey {actionName}: {ex.Message}");
            }
        }

        private (uint modifiers, uint keyCode) ParseHotkey(string hotkeyString)
        {
            uint modifiers = 0;
            uint keyCode = 0;

            var parts = hotkeyString.Split('+');
            foreach (var part in parts)
            {
                var trimmedPart = part.Trim();
                switch (trimmedPart.ToUpper())
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers |= 0x0003; // MOD_CONTROL
                        break;
                    case "SHIFT":
                        modifiers |= 0x0004; // MOD_SHIFT
                        break;
                    case "ALT":
                        modifiers |= 0x0001; // MOD_ALT
                        break;
                    default:
                        // Try to parse as a key
                        keyCode = GetKeyCode(trimmedPart);
                        break;
                }
            }

            return (modifiers, keyCode);
        }

        private uint GetKeyCode(string keyName)
        {
            // Map common key names to virtual key codes
            switch (keyName.ToUpper())
            {
                case "A": return 0x41;
                case "B": return 0x42;
                case "C": return 0x43;
                case "D": return 0x44;
                case "E": return 0x45;
                case "F": return 0x46;
                case "G": return 0x47;
                case "H": return 0x48;
                case "I": return 0x49;
                case "J": return 0x4A;
                case "K": return 0x4B;
                case "L": return 0x4C;
                case "M": return 0x4D;
                case "N": return 0x4E;
                case "O": return 0x4F;
                case "P": return 0x50;
                case "Q": return 0x51;
                case "R": return 0x52;
                case "S": return 0x53;
                case "T": return 0x54;
                case "U": return 0x55;
                case "V": return 0x56;
                case "W": return 0x57;
                case "X": return 0x58;
                case "Y": return 0x59;
                case "Z": return 0x5A;
                case "F1": return 0x70;
                case "F2": return 0x71;
                case "F3": return 0x72;
                case "F4": return 0x73;
                case "F5": return 0x74;
                case "F6": return 0x75;
                case "F7": return 0x76;
                case "F8": return 0x77;
                case "F9": return 0x78;
                case "F10": return 0x79;
                case "F11": return 0x7A;
                case "F12": return 0x7B;
                case "ESCAPE": return 0x1B;
                case "ENTER": return 0x0D;
                case "SPACE": return 0x20;
                case "TAB": return 0x09;
                case "BACKSPACE": return 0x08;
                case "DELETE": return 0x2E;
                case "INSERT": return 0x2D;
                case "HOME": return 0x24;
                case "END": return 0x23;
                case "PAGEUP": return 0x21;
                case "PAGEDOWN": return 0x22;
                case "UP": return 0x26;
                case "DOWN": return 0x28;
                case "LEFT": return 0x25;
                case "RIGHT": return 0x27;
                default:
                    // Try to parse as a number
                    if (uint.TryParse(keyName, out var num))
                        return num;
                    return 0;
            }
        }

        private Action GetActionForHotkey(string actionName)
        {
            return actionName switch
            {
                "RegionCapture" => () => OnRegionCaptureRequested?.Invoke(),
                "FullScreenCapture" => () => OnFullScreenCaptureRequested?.Invoke(),
                "ToggleRecording" => () => OnToggleRecordingRequested?.Invoke(),
                "Cancel" => () => OnCancelRequested?.Invoke(),
                "Save" => () => OnSaveRequested?.Invoke(),
                "Copy" => () => OnCopyRequested?.Invoke(),
                _ => () => { }
            };
        }

        public void HandleHotkeyMessage(int hotkeyId)
        {
            if (_registeredHotkeys.TryGetValue(hotkeyId, out var action))
            {
                action?.Invoke();
            }
        }

        private void UnregisterAllHotkeys()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                foreach (var hotkeyId in _registeredHotkeys.Keys)
                {
                    UnregisterHotKey(_windowHandle, hotkeyId);
                }
            }
            _registeredHotkeys.Clear();
            _hotkeyIds.Clear();
        }

        public void UpdateHotkeys()
        {
            if (_isInitialized && _settingsService.CurrentSettings.EnableGlobalHotkeys)
            {
                RegisterHotkeys();
            }
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
            _isInitialized = false;
        }

        // Events
        public event Action? OnRegionCaptureRequested;
        public event Action? OnFullScreenCaptureRequested;
        public event Action? OnPinScreenshotRequested;
        public event Action? OnToggleRecordingRequested;
        public event Action? OnCancelRequested;
        public event Action? OnSaveRequested;
        public event Action? OnCopyRequested;
    }
} 