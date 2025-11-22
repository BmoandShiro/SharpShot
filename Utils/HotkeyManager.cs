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
        private readonly Dictionary<string, int> _hotkeyPressCounts;
        private readonly Dictionary<string, DateTime> _hotkeyLastPressTimes;
        private readonly Dictionary<string, bool> _hotkeyToggleStates;
        private readonly Dictionary<string, string> _modifierHotkeys; // Track single modifier hotkeys that need hooks
        private int _nextHotkeyId = 1;
        private bool _isInitialized;
        private IntPtr _windowHandle;
        private IntPtr _keyboardHookHandle = IntPtr.Zero;
        private LowLevelKeyboardProc _keyboardProc;
        private const int TRIPLE_CLICK_TIMEOUT_MS = 500; // 500ms window for triple click
        private const int DOUBLE_CLICK_TIMEOUT_MS = 300; // 300ms window for double click
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private const int VK_MENU = 0x12; // Alt key

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public HotkeyManager(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _registeredHotkeys = new Dictionary<int, Action>();
            _hotkeyIds = new Dictionary<string, int>();
            _hotkeyPressCounts = new Dictionary<string, int>();
            _hotkeyLastPressTimes = new Dictionary<string, DateTime>();
            _hotkeyToggleStates = new Dictionary<string, bool>();
            _modifierHotkeys = new Dictionary<string, string>();
            _isInitialized = false;
            _keyboardProc = KeyboardHookProc;
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

            // Register hotkeys from settings - only register if they exist and are not empty
            if (_settingsService.CurrentSettings.Hotkeys.ContainsKey("ScreenshotRegion") && 
                !string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.Hotkeys["ScreenshotRegion"]))
                RegisterHotkey("ScreenshotRegion", _settingsService.CurrentSettings.Hotkeys["ScreenshotRegion"]);
            if (_settingsService.CurrentSettings.Hotkeys.ContainsKey("ScreenshotFullscreen") && 
                !string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.Hotkeys["ScreenshotFullscreen"]))
                RegisterHotkey("ScreenshotFullscreen", _settingsService.CurrentSettings.Hotkeys["ScreenshotFullscreen"]);
            if (_settingsService.CurrentSettings.Hotkeys.ContainsKey("RecordRegion") && 
                !string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.Hotkeys["RecordRegion"]))
                RegisterHotkey("RecordRegion", _settingsService.CurrentSettings.Hotkeys["RecordRegion"]);
            if (_settingsService.CurrentSettings.Hotkeys.ContainsKey("RecordFullscreen") && 
                !string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.Hotkeys["RecordFullscreen"]))
                RegisterHotkey("RecordFullscreen", _settingsService.CurrentSettings.Hotkeys["RecordFullscreen"]);
            if (_settingsService.CurrentSettings.Hotkeys.ContainsKey("Copy") && 
                !string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.Hotkeys["Copy"]))
                RegisterHotkey("Copy", _settingsService.CurrentSettings.Hotkeys["Copy"]);
            if (_settingsService.CurrentSettings.Hotkeys.ContainsKey("Save") && 
                !string.IsNullOrWhiteSpace(_settingsService.CurrentSettings.Hotkeys["Save"]))
                RegisterHotkey("Save", _settingsService.CurrentSettings.Hotkeys["Save"]);
        }

        private void RegisterHotkey(string actionName, string hotkeyString)
        {
            try
            {
                var (modifiers, keyCode) = ParseHotkey(hotkeyString);
                
                // Check if triple-click is enabled for this action
                bool tripleClickEnabled = _settingsService.CurrentSettings.Hotkeys.GetValueOrDefault($"{actionName}TripleClick", "false") == "true";
                
                // Prevent single modifier keys from being registered as global hotkeys UNLESS triple-click is enabled
                // Triple-click mode makes single modifiers safe since they require 3 presses
                if (IsSingleModifierKey(hotkeyString) && !tripleClickEnabled)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Cannot register '{hotkeyString}' as a global hotkey for {actionName} - it would interfere with normal typing. Use combinations like 'Ctrl+Shift+A', 'F1', or 'Space' instead. Or enable triple-click mode.");
                    return;
                }
                
                // For single modifier keys with triple-click enabled, use keyboard hook instead of RegisterHotKey
                if (IsSingleModifierKey(hotkeyString) && tripleClickEnabled)
                {
                    // Store the modifier hotkey for hook-based detection
                    _modifierHotkeys[actionName] = hotkeyString;
                    System.Diagnostics.Debug.WriteLine($"Registered modifier hotkey (hook-based): {actionName} = {hotkeyString}, Triple-click enabled: {tripleClickEnabled}");
                    
                    // Install keyboard hook if not already installed
                    if (_keyboardHookHandle == IntPtr.Zero)
                    {
                        InstallKeyboardHook();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Keyboard hook already installed, added {actionName} = {hotkeyString} to tracking list");
                    }
                    return;
                }
                
                // Validate hotkey - must have both modifiers and a key, or just a key
                if (keyCode == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid hotkey '{hotkeyString}' for {actionName}: Missing key code. Hotkeys must include a key (e.g., 'Ctrl+A', 'F1', 'Space')");
                    return;
                }

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

        private bool IsSingleModifierKey(string hotkeyString)
        {
            return hotkeyString == "Shift" || hotkeyString == "Ctrl" || 
                   hotkeyString == "Control" || hotkeyString == "Alt";
        }

        private (uint modifiers, uint keyCode) ParseHotkey(string hotkeyString)
        {
            uint modifiers = 0;
            uint keyCode = 0;

            // Prevent single modifier keys from being parsed as valid hotkeys
            if (hotkeyString == "Ctrl" || hotkeyString == "Control" || 
                hotkeyString == "Shift" || hotkeyString == "Alt")
            {
                // Return invalid combination to prevent registration
                return (0, 0);
            }

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
                case "F13": return 0x7C;
                case "F14": return 0x7D;
                case "F15": return 0x7E;
                case "F16": return 0x7F;
                case "F17": return 0x80;
                case "F18": return 0x81;
                case "F19": return 0x82;
                case "F20": return 0x83;
                case "F21": return 0x84;
                case "F22": return 0x85;
                case "F23": return 0x86;
                case "F24": return 0x87;
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
                "ScreenshotRegion" => () => OnRegionCaptureRequested?.Invoke(),
                "ScreenshotFullscreen" => () => OnFullScreenCaptureRequested?.Invoke(),
                "RecordRegion" => () => OnToggleRecordingRequested?.Invoke(),
                "RecordFullscreen" => () => OnToggleRecordingRequested?.Invoke(),
                "Save" => () => OnSaveRequested?.Invoke(),
                "Copy" => () => OnCopyRequested?.Invoke(),
                _ => () => { }
            };
        }

        public void HandleHotkeyMessage(int hotkeyId)
        {
            if (_registeredHotkeys.TryGetValue(hotkeyId, out var action))
            {
                // Find the action name for this hotkey ID
                string? actionName = null;
                foreach (var kvp in _hotkeyIds)
                {
                    if (kvp.Value == hotkeyId)
                    {
                        actionName = kvp.Key;
                        break;
                    }
                }

                if (actionName != null)
                {
                    // Check if triple-click is required for this action
                    bool requiresTripleClick = _settingsService.CurrentSettings.Hotkeys.GetValueOrDefault($"{actionName}TripleClick", "false") == "true";
                    
                    if (requiresTripleClick)
                    {
                        HandleTripleClickAction(actionName, action);
                    }
                    else if (actionName == "ScreenshotRegion")
                    {
                        // Special handling for region selection - implement toggle behavior
                        HandleRegionSelectionToggle();
                    }
                    else
                    {
                        // Normal single-click behavior for other actions
                        action?.Invoke();
                    }
                }
                else
                {
                    // Fallback to normal behavior if action name not found
                    action?.Invoke();
                }
            }
        }

        private void HandleTripleClickAction(string actionName, Action action)
        {
            var now = DateTime.Now;
            
            // Initialize if not exists
            if (!_hotkeyPressCounts.ContainsKey(actionName))
            {
                _hotkeyPressCounts[actionName] = 0;
                _hotkeyLastPressTimes[actionName] = now;
            }

            var lastPressTime = _hotkeyLastPressTimes[actionName];
            var timeSinceLastPress = (now - lastPressTime).TotalMilliseconds;

            // Reset counter if too much time has passed
            if (timeSinceLastPress > TRIPLE_CLICK_TIMEOUT_MS)
            {
                _hotkeyPressCounts[actionName] = 0;
            }

            // Increment press count
            _hotkeyPressCounts[actionName]++;
            _hotkeyLastPressTimes[actionName] = now;

            // Check if we have 3 presses within the timeout window
            if (_hotkeyPressCounts[actionName] >= 3)
            {
                System.Diagnostics.Debug.WriteLine($"Triple-click detected for {actionName}");
                
                // Special handling for region selection - implement toggle behavior
                if (actionName == "ScreenshotRegion")
                {
                    // Check if there's already an active region selection
                    if (_hotkeyToggleStates.ContainsKey(actionName) && _hotkeyToggleStates[actionName])
                    {
                        // There's already an active region selection - cancel it
                        System.Diagnostics.Debug.WriteLine("Triple-click while region selection is active - canceling existing instance");
                        OnRegionCaptureCanceled?.Invoke();
                        
                        // Reset the toggle state
                        _hotkeyToggleStates[actionName] = false;
                    }
                    else
                    {
                        // No active region selection - start it
                        System.Diagnostics.Debug.WriteLine("Triple-click - starting region selection");
                        _hotkeyToggleStates[actionName] = true;
                        action?.Invoke();
                    }
                }
                else
                {
                    // Normal behavior for other actions
                    action?.Invoke();
                }
                
                // Reset counter after successful triple-click
                _hotkeyPressCounts[actionName] = 0;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Triple-click progress for {actionName}: {_hotkeyPressCounts[actionName]}/3");
            }
        }

        private void HandleRegionSelectionToggle()
        {
            var now = DateTime.Now;
            var actionName = "ScreenshotRegion";
            
            // Initialize if not exists
            if (!_hotkeyPressCounts.ContainsKey(actionName))
            {
                _hotkeyPressCounts[actionName] = 0;
                _hotkeyLastPressTimes[actionName] = now;
            }

            var lastPressTime = _hotkeyLastPressTimes[actionName];
            var timeSinceLastPress = (now - lastPressTime).TotalMilliseconds;

            // Reset counter if too much time has passed
            if (timeSinceLastPress > DOUBLE_CLICK_TIMEOUT_MS)
            {
                _hotkeyPressCounts[actionName] = 0;
            }

            // Increment press count
            _hotkeyPressCounts[actionName]++;
            _hotkeyLastPressTimes[actionName] = now;

            if (_hotkeyPressCounts[actionName] == 1)
            {
                // Check if there's already an active region selection window
                if (_hotkeyToggleStates.ContainsKey(actionName) && _hotkeyToggleStates[actionName])
                {
                    // There's already an active region selection - cancel it first
                    System.Diagnostics.Debug.WriteLine("F18 pressed while region selection is active - canceling existing instance first");
                    OnRegionCaptureCanceled?.Invoke();
                    
                    // Reset the toggle state
                    _hotkeyToggleStates[actionName] = false;
                    _hotkeyPressCounts[actionName] = 0;
                    return;
                }
                
                // First press - start region selection
                System.Diagnostics.Debug.WriteLine("First press of F18 - starting region selection");
                _hotkeyToggleStates[actionName] = true;
                OnRegionCaptureRequested?.Invoke();
            }
            else if (_hotkeyPressCounts[actionName] == 2 && timeSinceLastPress <= DOUBLE_CLICK_TIMEOUT_MS)
            {
                // Second press within timeout - cancel region selection
                System.Diagnostics.Debug.WriteLine("Second press of F18 - canceling region selection");
                _hotkeyToggleStates[actionName] = false;
                _hotkeyPressCounts[actionName] = 0;
                OnRegionCaptureCanceled?.Invoke();
            }
            else if (_hotkeyPressCounts[actionName] >= 3)
            {
                // Reset for next cycle
                _hotkeyPressCounts[actionName] = 0;
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
            _hotkeyPressCounts.Clear();
            _hotkeyLastPressTimes.Clear();
            _hotkeyToggleStates.Clear();
            
            // Uninstall keyboard hook before clearing modifier hotkeys
            if (_modifierHotkeys.Count > 0)
            {
                UninstallKeyboardHook();
            }
            _modifierHotkeys.Clear();
        }
        
        private void InstallKeyboardHook()
        {
            if (_keyboardHookHandle != IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Keyboard hook already installed");
                return;
            }
            
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                _keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
            
            if (_keyboardHookHandle == IntPtr.Zero)
            {
                uint error = GetLastError();
                System.Diagnostics.Debug.WriteLine($"Failed to install keyboard hook. Error code: {error}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Keyboard hook installed successfully. Handle: {_keyboardHookHandle}, Tracking {_modifierHotkeys.Count} modifier hotkeys");
                foreach (var kvp in _modifierHotkeys)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {kvp.Key}: {kvp.Value}");
                }
            }
        }
        
        private void UninstallKeyboardHook()
        {
            if (_keyboardHookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = IntPtr.Zero;
                System.Diagnostics.Debug.WriteLine("Keyboard hook uninstalled");
            }
        }
        
        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // Always call next hook first (required by Windows)
            IntPtr result = CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
            
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN && _modifierHotkeys.Count > 0)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                uint vkCode = hookStruct.vkCode;
                
                // Check if any other modifier keys are currently pressed
                // We only want to trigger if ONLY the target modifier is pressed
                bool ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                bool shiftPressed = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                bool altPressed = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                
                System.Diagnostics.Debug.WriteLine($"Hook: vkCode=0x{vkCode:X2}, Ctrl={ctrlPressed}, Shift={shiftPressed}, Alt={altPressed}, Tracking {_modifierHotkeys.Count} modifiers");
                
                // Check if this is a modifier key we're tracking
                foreach (var kvp in _modifierHotkeys)
                {
                    string actionName = kvp.Key;
                    string modifierName = kvp.Value;
                    bool isTargetModifier = false;
                    
                    if (modifierName == "Ctrl" || modifierName == "Control")
                    {
                        isTargetModifier = (vkCode == VK_CONTROL || vkCode == 0xA2 || vkCode == 0xA3); // Left/Right Ctrl
                    }
                    else if (modifierName == "Shift")
                    {
                        isTargetModifier = (vkCode == VK_SHIFT || vkCode == 0xA0 || vkCode == 0xA1); // Left/Right Shift
                    }
                    else if (modifierName == "Alt")
                    {
                        isTargetModifier = (vkCode == VK_MENU || vkCode == 0xA4 || vkCode == 0xA5); // Left/Right Alt
                    }
                    
                    if (isTargetModifier)
                    {
                        System.Diagnostics.Debug.WriteLine($"Hook: Matched {modifierName} for {actionName}");
                        
                        // Only trigger if ONLY the target modifier is pressed (no other modifiers)
                        bool otherModifiersPressed = false;
                        if (modifierName == "Ctrl" || modifierName == "Control")
                        {
                            otherModifiersPressed = shiftPressed || altPressed;
                        }
                        else if (modifierName == "Shift")
                        {
                            otherModifiersPressed = ctrlPressed || altPressed;
                        }
                        else if (modifierName == "Alt")
                        {
                            otherModifiersPressed = ctrlPressed || shiftPressed;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Hook: Other modifiers pressed: {otherModifiersPressed}");
                        
                        if (!otherModifiersPressed)
                        {
                            // Check if triple-click is enabled
                            bool tripleClickEnabled = _settingsService.CurrentSettings.Hotkeys.GetValueOrDefault($"{actionName}TripleClick", "false") == "true";
                            
                            System.Diagnostics.Debug.WriteLine($"Hook: Processing {modifierName} for {actionName}, triple-click enabled: {tripleClickEnabled}");
                            
                            if (tripleClickEnabled)
                            {
                                // Get the action and handle triple-click
                                var action = GetActionForHotkey(actionName);
                                if (action != null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Hook: Calling HandleTripleClickAction for {actionName}");
                                    HandleTripleClickAction(actionName, action);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Hook: No action found for {actionName}");
                                }
                            }
                        }
                        break;
                    }
                }
            }
            
            return result;
        }
        
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public void UpdateHotkeys()
        {
            if (!_isInitialized) return;
            
            // Always unregister all hotkeys first
            UnregisterAllHotkeys();
            
            // Only register hotkeys if global hotkeys are enabled
            if (_settingsService.CurrentSettings.EnableGlobalHotkeys)
            {
                RegisterHotkeys();
            }
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
            UninstallKeyboardHook();
            _isInitialized = false;
        }

        public void DebugHotkeyStatus()
        {
            System.Diagnostics.Debug.WriteLine($"=== Hotkey Manager Debug Info ===");
            System.Diagnostics.Debug.WriteLine($"Initialized: {_isInitialized}");
            System.Diagnostics.Debug.WriteLine($"Window Handle: {_windowHandle}");
            System.Diagnostics.Debug.WriteLine($"Global Hotkeys Enabled: {_settingsService.CurrentSettings.EnableGlobalHotkeys}");
            System.Diagnostics.Debug.WriteLine($"Registered Hotkeys Count: {_registeredHotkeys.Count}");
            
            foreach (var hotkey in _settingsService.CurrentSettings.Hotkeys)
            {
                System.Diagnostics.Debug.WriteLine($"Setting: {hotkey.Key} = '{hotkey.Value}'");
            }
            
            foreach (var registered in _registeredHotkeys)
            {
                var actionName = _hotkeyIds.FirstOrDefault(x => x.Value == registered.Key).Key ?? "Unknown";
                System.Diagnostics.Debug.WriteLine($"Registered: {actionName} (ID: {registered.Key})");
            }

            foreach (var toggleState in _hotkeyToggleStates)
            {
                System.Diagnostics.Debug.WriteLine($"Toggle State: {toggleState.Key} = {toggleState.Value}");
            }
            System.Diagnostics.Debug.WriteLine($"================================");
        }

        public void ResetRegionSelectionToggle()
        {
            var actionName = "ScreenshotRegion";
            if (_hotkeyToggleStates.ContainsKey(actionName))
            {
                _hotkeyToggleStates[actionName] = false;
                System.Diagnostics.Debug.WriteLine("Region selection toggle state reset");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ResetRegionSelectionToggle called but no toggle state found");
            }
        }

        public void SetRegionSelectionActive()
        {
            var actionName = "ScreenshotRegion";
            _hotkeyToggleStates[actionName] = true;
            System.Diagnostics.Debug.WriteLine("Region selection toggle state set to active (started via button)");
        }

        // Events
        public event Action? OnRegionCaptureRequested;
        public event Action? OnRegionCaptureCanceled;
        public event Action? OnFullScreenCaptureRequested;
        public event Action? OnToggleRecordingRequested;
        public event Action? OnSaveRequested;
        public event Action? OnCopyRequested;
    }
} 