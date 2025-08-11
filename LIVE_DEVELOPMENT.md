# SharpShot Live Development Environment

This document describes the new live development environment that provides hot reloading capabilities without rebuilding Docker images.

## Overview

The live development environment uses `dotnet watch` inside a Docker container to automatically rebuild your project whenever you make code changes. This provides a consistent development environment while maintaining fast iteration cycles.

## Quick Start

### Option 1: Using the Batch File
```bash
# From the root directory
"Start Live Dev Environment.bat"
```

### Option 2: Using PowerShell
```powershell
# From the SharpShot directory
.\start-live-dev.ps1
```

### Option 3: Using the Management Script
```powershell
# Start the environment
.\manage-live-dev.ps1 start

# Check status
.\manage-live-dev.ps1 status

# View logs
.\manage-live-dev.ps1 logs

# Stop the environment
.\manage-live-dev.ps1 stop
```

## How It Works

1. **Docker Container**: A Linux container runs with your source code mounted as a volume
2. **Enhanced File Watching**: Custom script watches for ALL file changes including assets (icons, images, etc.)
3. **Asset File Watcher**: Windows PowerShell script watches for asset file changes and copies them automatically
4. **Volume Mounts**: Build artifacts are shared between the container and your Windows host
5. **Local Execution**: You run the application locally using `dotnet run`

## Development Workflow

1. **Start the environment**:
   ```bash
   "Start Live Dev Environment.bat"
   ```

2. **Make code changes** in your favorite editor

3. **Docker automatically rebuilds** the project when files change

4. **For icon changes**: The application will automatically restart when XAML files change

5. **Run the application locally**:
   ```bash
   dotnet run
   ```

### Special Note: Icon Changes

Since SharpShot uses SVG-style geometry paths for icons (not image files), icon changes require the application to restart to be visible. The enhanced setup includes:

- **Automatic XAML watching**: Changes to `.xaml` files trigger app restarts
- **Manual restart option**: Use `"Restart App for Icon Changes.bat"` for immediate restarts
- **Background watchers**: Both asset files and XAML files are monitored

## Available Commands

### Management Script Commands
```powershell
.\manage-live-dev.ps1 start    # Start the live development environment
.\manage-live-dev.ps1 stop     # Stop the environment
.\manage-live-dev.ps1 restart  # Restart the environment
.\manage-live-dev.ps1 logs     # View container logs
.\manage-live-dev.ps1 build    # Manually build the project
.\manage-live-dev.ps1 run      # Run the application locally
.\manage-live-dev.ps1 status   # Check environment status
.\manage-live-dev.ps1 clean    # Clean up Docker resources
```

### Manual Docker Commands
```bash
# Start the environment
docker compose -f docker-compose.live-dev.yml up --build -d

# View logs
docker logs -f sharpshot-live-development

# Build manually
docker exec sharpshot-live-development dotnet build

# Stop the environment
docker compose -f docker-compose.live-dev.yml down
```

### Icon Change Commands
```bash
# Quick restart for icon changes
"Restart App for Icon Changes.bat"

# Force refresh resources
.\force-refresh.ps1 refresh

# Watch for XAML changes
.\force-refresh.ps1 watch
```

## Benefits

✅ **Live Code Updates**: No need to rebuild Docker images for code changes  
✅ **Asset File Watching**: Icons, images, and other assets are automatically detected and copied  
✅ **Comprehensive File Monitoring**: Watches for ALL file types including assets  
✅ **Consistent Environment**: Same .NET version and dependencies across all machines  
✅ **Fast Iteration**: Automatic rebuilds when files change  
✅ **Shared Build Cache**: Faster builds with cached dependencies  
✅ **Windows GUI Support**: Application runs on Windows host for proper GUI display  

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Windows Host                            │
│  ┌─────────────────┐    ┌─────────────────────────────┐   │
│  │   Your Editor   │    │      SharpShot App         │   │
│  │   (VS Code,     │    │     (dotnet run)           │   │
│  │    Visual       │    │                             │   │
│  │    Studio)      │    └─────────────────────────────┘   │
│  └─────────────────┘                                      │
│           │                                                │
│           ▼                                                │
│  ┌─────────────────┐    ┌─────────────────────────────┐   │
│  │   Source Code   │◄──►│    Docker Container        │   │
│  │   (Volume       │    │   (dotnet watch build)     │   │
│  │    Mount)       │    │                             │   │
│  └─────────────────┘    └─────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Troubleshooting

### Container Not Starting
```bash
# Check Docker logs
docker logs sharpshot-live-development

# Restart the environment
.\manage-live-dev.ps1 restart
```

### Build Failures
```bash
# Clean and rebuild
.\manage-live-dev.ps1 clean
.\manage-live-dev.ps1 start
```

### Performance Issues
- The first build may take longer due to dependency restoration
- Subsequent builds should be faster due to caching
- If builds are slow, try cleaning the environment and restarting

### File Permission Issues
- Ensure Docker has access to your project directory
- On Windows, make sure Docker Desktop has access to your drives

## Comparison with Previous Setup

| Feature | Previous Setup | Live Development |
|---------|----------------|------------------|
| Code Changes | Manual rebuild required | Automatic rebuild |
| Docker Rebuild | Every code change | Only for dependency changes |
| Build Speed | Slower (full rebuild) | Faster (incremental) |
| Development Speed | Slower iteration | Fast iteration |
| Environment | Consistent | Consistent |

## Files Created

- `docker-compose.live-dev.yml` - Live development Docker Compose configuration
- `Dockerfile.live-dev` - Live development Dockerfile with dotnet watch
- `start-live-dev.ps1` - PowerShell script for starting live development
- `manage-live-dev.ps1` - Management script for various commands
- `"Start Live Dev Environment.bat"` - Batch file for easy startup
- `LIVE_DEVELOPMENT.md` - This documentation file 