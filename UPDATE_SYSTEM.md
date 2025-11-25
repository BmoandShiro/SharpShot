# SharpShot Auto-Update System

## Overview

SharpShot includes an automatic update system that checks for new releases and allows users to update the application seamlessly.

## Features

- **Automatic Update Checks**: Checks for updates once per day (configurable)
- **Manual Update Check**: Users can manually check for updates from Settings
- **Seamless Updates**: Downloads and applies updates automatically
- **Settings Preservation**: User settings are preserved during updates
- **Progress Tracking**: Shows download and installation progress

## Configuration

### GitHub Repository Setup

Before using the update system, you need to configure your GitHub repository information:

1. **Update Default Repository** (in `UpdateService.cs`):
   ```csharp
   private const string DefaultRepoOwner = "YourGitHubUsername"; // Change this
   private const string DefaultRepoName = "SharpShot"; // Change if different
   ```

2. **Or Configure in Settings**:
   Users can set custom repository owner/name in Settings (if you add UI for it)

### Release Requirements

For the update system to work, your GitHub releases must:

1. **Tag Format**: Use semantic versioning (e.g., `v1.2.3` or `1.2.3`)
2. **Release Assets**: Include a `.zip` file as a release asset
3. **Zip Structure**: The zip should contain the SharpShot folder with all files

**Recommended Zip Structure:**
```
SharpShot-Release-v1.2.3.zip
└── SharpShot/
    ├── SharpShot.exe
    ├── ffmpeg/
    ├── OBS-Studio/
    └── ... (other files)
```

## How It Works

### Update Check Flow

1. **On Startup**: If auto-update is enabled, checks for updates in background
2. **Manual Check**: User clicks "Check Now" in Settings
3. **Version Comparison**: Compares current version with latest GitHub release
4. **Update Notification**: Shows UpdateWindow if newer version is available

### Update Process

1. **Download**: Downloads the release zip to temp directory
2. **Extract**: Extracts zip to temp folder
3. **Create Script**: Generates PowerShell script to apply update
4. **Shutdown**: Closes current application
5. **Apply Update**: Script replaces files (preserving settings)
6. **Restart**: Launches updated application

### File Preservation

The update script preserves:
- `settings.json` (user settings)
- `OBS-Studio/` folder (if user has custom OBS config)
- `ffmpeg/` folder (if user has custom FFmpeg)
- `*.log` files

## User Settings

Users can control update behavior in Settings:

- **Enable Auto-Update Check**: Toggle automatic update checking
- **Check Now Button**: Manually trigger update check

## Update Window

The UpdateWindow shows:
- New version number
- Release name
- Release notes
- Download/Install progress
- Update/Later buttons

## Technical Details

### Update Service

- **UpdateService.cs**: Core update logic
  - Checks GitHub Releases API
  - Downloads and extracts updates
  - Creates update script

### Update Script

The update script (`apply_update.ps1`) is generated dynamically and:
- Waits for application to close
- Copies new files (excluding preserved items)
- Cleans up temp files
- Restarts application

### Version Detection

- Uses `Assembly.GetName().Version` for current version
- Parses GitHub release tag for latest version
- Compares versions to determine if update is needed

## Troubleshooting

### Update Not Detected

- Check GitHub repository is correctly configured
- Verify release tag format (should be parseable as version)
- Ensure release has a `.zip` asset

### Update Fails

- Check internet connection
- Verify GitHub API is accessible
- Check temp directory permissions
- Review update script logs

### Settings Lost

- Settings should be preserved automatically
- If lost, check `%APPDATA%\SharpShot\settings.json` backup
- Update script explicitly excludes `settings.json` from replacement

## Security Considerations

- **HTTPS Only**: All downloads use HTTPS
- **GitHub API**: Uses official GitHub Releases API
- **No Code Execution**: Update script only copies files
- **User Confirmation**: User must click "Update Now" to proceed

## Future Enhancements

Potential improvements:
- Delta updates (only download changed files)
- Rollback capability
- Update scheduling
- Silent updates (with user permission)
- Update channels (stable/beta)

