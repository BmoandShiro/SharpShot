# SharpShot - Dark-Themed Screenshot & Screen Recording Tool

A modern, dark-themed screenshot and screen recording tool built with C# and WPF for Windows 11.

## Features

### 🖼️ Screenshot Functionality
- **Full Screen Capture**: Capture entire screen with one click
- **Region Selection**: Click-and-drag to select specific areas (coming soon)
- **Multiple Formats**: Save as PNG, JPG, or BMP
- **Copy to Clipboard**: Instantly copy screenshots to clipboard
- **Visual Feedback**: Brief flash overlay when capturing

### 🎥 Screen Recording
- **Full Screen Recording**: Record entire screen
- **Region Recording**: Record specific areas (coming soon)
- **Audio Support**: Include microphone audio in recordings
- **Timer Display**: Real-time recording duration
- **Visual Indicators**: Button changes color and shows timer when recording

### 🎨 Modern Dark UI
- **Floating Toolbar**: Always-on-top, draggable toolbar
- **Dark Theme**: Beautiful dark interface with accent colors
- **Hover Effects**: Smooth animations and visual feedback
- **Rounded Corners**: Modern, polished appearance

### ⚙️ Settings & Configuration
- **Customizable Save Path**: Choose where to save files
- **Format Selection**: PNG, JPG, BMP for screenshots
- **Video Quality**: High, Medium, Low quality options
- **Audio Recording**: Toggle microphone recording
- **Start Minimized**: Launch hidden in system tray

### ⌨️ Global Hotkeys (Coming Soon)
- **Double Ctrl**: Region capture
- **Ctrl+Shift+S**: Full screen capture
- **Ctrl+Shift+R**: Toggle recording
- **Customizable**: All hotkeys can be rebinded

## Installation & Usage

### Prerequisites
- Windows 10/11
- .NET 8.0 Runtime

### Building from Source
```bash
# Clone the repository
git clone <repository-url>
cd SharpShot

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run
```

### Running the Application
1. **Launch**: Run `dotnet run` from the project directory
2. **Toolbar**: A floating toolbar will appear in the top-right corner
3. **Screenshot**: Click the 📸 button or use Ctrl+Shift+S
4. **Recording**: Click the 🎥 button or use Ctrl+Shift+R
5. **Settings**: Click the ⚙️ button to configure options
6. **Close**: Click the ❌ button to exit

## Project Structure

```
SharpShot/
├── App.xaml                 # Application entry point and resources
├── App.xaml.cs              # Application lifecycle management
├── MainWindow.xaml          # Main floating toolbar UI
├── MainWindow.xaml.cs       # Main window logic
├── Models/
│   └── Settings.cs          # Settings model with INotifyPropertyChanged
├── Services/
│   ├── SettingsService.cs   # Settings loading/saving
│   ├── ScreenshotService.cs # Screenshot capture logic
│   └── RecordingService.cs  # Screen recording logic
├── Utils/
│   └── HotkeyManager.cs     # Global hotkey management
└── UI/
    ├── SettingsWindow.xaml  # Settings dialog UI
    └── SettingsWindow.xaml.cs # Settings dialog logic
```

## Features in Development

### 🚧 Coming Soon
- **Region Selection Overlay**: Click-and-drag area selection
- **Annotation Tools**: Drawing, arrows, text on screenshots
- **Global Hotkeys**: Full keyboard shortcut support
- **System Tray**: Minimize to system tray with notifications
- **Video Recording**: Actual screen recording implementation
- **Pin Screenshots**: Pin captured images to screen
- **Toast Notifications**: Modern notification system

### 🔧 Technical Improvements
- **ScreenRecorderLib**: Proper video recording implementation
- **Gma.System.MouseKeyHook**: Global hotkey registration
- **Multi-Monitor Support**: Capture across multiple displays
- **DPI Scaling**: High-DPI display support
- **Performance Optimization**: Faster capture and processing

## Configuration

Settings are automatically saved to:
```
%APPDATA%\SharpShot\settings.json
```

### Default Settings
- **Save Path**: `%USERPROFILE%\Pictures\SharpShot`
- **Screenshot Format**: PNG
- **Video Quality**: High
- **Audio Recording**: Enabled
- **Global Hotkeys**: Enabled
- **Start Minimized**: Disabled

## Troubleshooting

### Common Issues

1. **Application won't start**
   - Ensure .NET 8.0 Runtime is installed
   - Check Windows compatibility

2. **Screenshots not saving**
   - Verify save path exists and is writable
   - Check disk space

3. **Recording not working**
   - Ensure microphone permissions are granted
   - Check audio drivers

4. **Hotkeys not responding**
   - Global hotkeys are not yet implemented
   - Use toolbar buttons for now

### Build Issues

If you encounter build errors:
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

## Contributing

This is a work in progress! The core functionality is implemented, but several features are still in development:

- ✅ Dark-themed floating toolbar
- ✅ Screenshot capture (full screen)
- ✅ Settings dialog
- ✅ Basic recording framework
- 🚧 Region selection
- 🚧 Global hotkeys
- 🚧 Annotation tools
- 🚧 Video recording implementation

## License

This project is open source and available under the MIT License.

---

**SharpShot** - Modern screenshot and screen recording for Windows 11 