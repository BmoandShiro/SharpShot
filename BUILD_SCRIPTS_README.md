# SharpShot Build Scripts - Complete Bundle

This directory contains build scripts that automatically bundle **both FFmpeg and OBS Studio** with SharpShot, using Docker for reliable building since local builds don't work.

## 🚀 Quick Start

### Option 1: Complete Bundle (Recommended)
```batch
# Double-click this file:
Build Complete Bundle.bat
```
This will:
- ✅ Build SharpShot in Docker
- ✅ Bundle FFmpeg
- ✅ Bundle OBS Studio  
- ✅ Create a ZIP distribution package
- ✅ Launch the application after build

### Option 2: Build and Bundle Only
```batch
# Double-click this file:
Build and Bundle Only.bat
```
This will:
- ✅ Build SharpShot in Docker
- ✅ Bundle FFmpeg
- ✅ Bundle OBS Studio
- ❌ No ZIP creation
- ❌ No auto-launch

### Option 3: FFmpeg Only (No OBS)
```batch
# Double-click this file:
Build with FFmpeg Only.bat
```
This will:
- ✅ Build SharpShot in Docker
- ✅ Bundle FFmpeg
- ❌ Skip OBS Studio
- ❌ No ZIP creation
- ❌ No auto-launch

## 🔧 Advanced Usage

### PowerShell Script Options
You can also run the PowerShell script directly with custom parameters:

```powershell
# Basic build and bundle
.\build-bundle-complete.ps1

# Build with custom configuration
.\build-bundle-complete.ps1 -Configuration Debug -Platform x64

# Skip Docker (local build - may not work)
.\build-bundle-complete.ps1 -SkipDocker

# Skip OBS Studio bundling
.\build-bundle-complete.ps1 -SkipOBS

# Skip FFmpeg bundling  
.\build-bundle-complete.ps1 -SkipFFmpeg

# Create ZIP distribution package
.\build-bundle-complete.ps1 -CreateZip

# Launch after build
.\build-bundle-complete.ps1 -LaunchAfterBuild

# Combine multiple options
.\build-bundle-complete.ps1 -CreateZip -LaunchAfterBuild -Configuration Release
```

## 📋 Prerequisites

### Required
- ✅ **Docker Desktop** running
- ✅ **FFmpeg** extracted to `ffmpeg/` directory
- ✅ **OBS Studio** extracted to `OBS-Studio/` directory

### Optional
- **OBS Studio**: Download from [obsproject.com](https://obsproject.com/) and extract to `OBS-Studio/`
- **FFmpeg**: Download from [ffmpeg.org](https://ffmpeg.org/download.html) and extract to `ffmpeg/`

## 🐳 How It Works

1. **Docker Environment**: Uses your existing `docker-compose.dev.yml` setup
2. **Clean Build**: Stops existing containers and starts fresh
3. **Build in Container**: Builds SharpShot inside Docker for consistency
4. **Bundle Dependencies**: Copies FFmpeg and OBS Studio to output directory
5. **Optional ZIP**: Creates distribution package if requested
6. **Optional Launch**: Runs the application after successful build

## 📁 Output Structure

After a successful build, you'll have:

```
bin/Release/net8.0-windows/x64/
├── SharpShot.exe
├── SharpShot.dll
├── ffmpeg/
│   ├── ffmpeg.exe
│   ├── ffprobe.exe
│   └── ffplay.exe
└── OBS-Studio/
    ├── bin/64bit/
    ├── data/
    └── ... (OBS files)
```

## 🚨 Troubleshooting

### Docker Issues
```bash
# Check if Docker is running
docker version

# View container logs
docker logs -f sharpshot-development

# Restart Docker environment
docker compose -f docker-compose.dev.yml down
docker compose -f docker-compose.dev.yml up --build -d
```

### Build Issues
- **Local builds don't work**: Use Docker (default behavior)
- **OBS Studio locked**: Close OBS Studio before building
- **FFmpeg missing**: Ensure `ffmpeg/bin/ffmpeg.exe` exists
- **Permission errors**: Run as Administrator if needed

### Common Errors
- **"Docker is not running"**: Start Docker Desktop
- **"OBS-Studio directory not found"**: Extract OBS Studio first
- **"FFmpeg not found"**: Extract FFmpeg to `ffmpeg/` directory

## 🔄 Workflow

### Daily Development
```batch
# Quick build and test
Build and Bundle Only.bat
```

### Release Build
```batch
# Complete package with ZIP
Build Complete Bundle.bat
```

### Testing Without OBS
```batch
# FFmpeg only for testing
Build with FFmpeg Only.bat
```

## 📦 Distribution

When using `-CreateZip`, the script creates:
- **Complete ZIP package** with all dependencies
- **Launcher script** (`Run SharpShot.bat`)
- **Documentation** (LICENSE, README, etc.)
- **Self-contained** application ready for distribution

## 🎯 Benefits

- ✅ **Reliable builds** using Docker
- ✅ **Automatic bundling** of FFmpeg and OBS Studio
- ✅ **No manual copying** of dependencies
- ✅ **Consistent output** across different machines
- ✅ **Distribution ready** with optional ZIP creation
- ✅ **Flexible options** for different build scenarios

## 🔗 Related Scripts

- **`obs-docker.ps1`**: Original OBS Docker build script
- **`build-with-obs.ps1`**: Legacy OBS-only build script
- **`extract-obs.ps1`**: Download and extract OBS Studio
- **`update-ffmpeg-wasapi.ps1`**: Update FFmpeg with WASAPI support

---

**Note**: These scripts are designed to work with your existing Docker setup. If you encounter issues, the original `OBS Docker.bat` approach should still work as a fallback.
