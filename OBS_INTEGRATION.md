# OBS Integration for SharpShot

## Overview

SharpShot now supports OBS Studio as a recording engine option, providing superior audio recording capabilities compared to the default ScreenRecorderLib.

## Benefits of OBS Integration

### Audio Quality
- **Professional-grade audio encoding** with multiple codec options (AAC, Opus, MP3)
- **Advanced audio mixing** with multiple tracks
- **Real-time audio monitoring** with VU meters
- **Audio filters** including noise suppression, compression, and EQ

### Device Management
- **Native WASAPI support** for better Windows audio integration
- **Automatic device detection** and management
- **Better handling of virtual audio devices** (Voicemeeter, VB-Audio, etc.)
- **Audio routing** support for complex setups

### Professional Features
- **Audio ducking** (lower music when speaking)
- **Audio delay compensation** for perfect sync
- **Multi-track recording** capabilities
- **Audio monitoring** with headphones

## Installation

### 1. Install OBS Studio
1. Download OBS Studio from [https://obsproject.com/](https://obsproject.com/)
2. Install with default settings
3. Launch OBS Studio at least once to create configuration files

### 2. Configure OBS WebSocket Server
1. Open OBS Studio
2. Go to **Tools** > **WebSocket Server Settings**
3. Check **Enable WebSocket server**
4. Set **Server Port** to `4444`
5. Set **Server Password** (optional - leave blank for no password)
6. Click **OK** to save settings

### 3. Configure SharpShot
1. Open SharpShot Settings
2. Set **Recording Engine** to "OBS"
3. Configure your audio recording mode:
   - **No Audio**: Video only
   - **System Audio Only**: Capture system audio
   - **Microphone Only**: Capture microphone input
   - **System Audio + Microphone**: Capture both

## Usage

### Starting a Recording
1. Select "OBS" as your recording engine in settings
2. Choose your audio recording mode
3. Use your hotkey or UI to start recording
4. SharpShot will automatically:
   - Check if OBS is running
   - Start OBS if needed
   - Connect to OBS WebSocket
   - Configure recording settings
   - Start the recording

### Stopping a Recording
1. Use your hotkey or UI to stop recording
2. SharpShot will automatically stop the OBS recording
3. The video file will be saved to your configured save path

## Troubleshooting

### OBS Not Found
- Ensure OBS Studio is installed in the default location
- Check that the installation completed successfully
- Try reinstalling OBS Studio

### WebSocket Connection Failed
- Verify OBS WebSocket server is enabled
- Check that port 4444 is not blocked by firewall
- Ensure OBS Studio is running
- Try restarting OBS Studio

### Recording Quality Issues
- Check OBS Studio settings for video quality
- Verify audio device selection in OBS
- Ensure sufficient disk space for recordings

### Audio Not Recording
- Check audio device selection in SharpShot settings
- Verify audio devices are working in OBS Studio
- Ensure audio sources are properly configured in OBS

## Technical Details

### WebSocket API
SharpShot communicates with OBS using the WebSocket API on port 4444. The integration supports:
- Starting/stopping recordings
- Setting recording paths
- Basic status monitoring

### Automatic OBS Management
- **Auto-start**: SharpShot can automatically start OBS if not running
- **Auto-configuration**: Recording settings are automatically configured
- **Connection management**: Automatic WebSocket connection handling

### Logging
OBS integration logs are saved to `obs_debug.log` in the SharpShot directory. This file contains:
- Connection attempts and results
- Recording start/stop events
- Error messages and debugging information

## Comparison with Other Recording Engines

| Feature | ScreenRecorderLib | OBS | FFmpeg |
|---------|------------------|-----|--------|
| Audio Quality | Basic | Professional | Good |
| Audio Mixing | Limited | Advanced | Basic |
| Device Management | Complex | Native | Complex |
| Audio Filters | None | Built-in | External |
| Real-time Monitoring | No | Yes | No |
| Multi-track | No | Yes | Limited |
| Setup Complexity | Low | Medium | High |

## Future Enhancements

Planned improvements for OBS integration:
- **Advanced scene management** - Configure OBS scenes automatically
- **Source management** - Add/remove video and audio sources
- **Filter configuration** - Set up audio filters programmatically
- **Streaming support** - Integrate with OBS streaming capabilities
- **Profile management** - Switch between OBS profiles

## Support

For issues with OBS integration:
1. Check the `obs_debug.log` file for error messages
2. Verify OBS Studio is properly configured
3. Test OBS WebSocket connection manually
4. Ensure all audio devices are working in OBS Studio

For general SharpShot support, refer to the main README.md file. 