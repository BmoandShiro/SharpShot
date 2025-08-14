# MSIX Deployment Guide for Microsoft Store

This guide explains how to create and deploy SharpShot to the Microsoft Store using MSIX packaging.

## Prerequisites

1. **Windows 10/11 SDK** - Install the latest Windows SDK from Microsoft
2. **Developer Account** - Microsoft Store developer account ($19 one-time fee)
3. **Code Signing Certificate** - Required for MSIX packages
4. **Visual Studio 2022** (optional but recommended)

## Quick Start

### Option 1: Using the Batch File
1. Double-click `Build MSIX for Store.bat`
2. Wait for the build to complete
3. Check the `bin\Release` folder for output files

### Option 2: Using PowerShell
```powershell
.\build-msix.ps1 -Configuration Release -Platform x64
```

### Option 3: Using Visual Studio
1. Right-click on the project in Solution Explorer
2. Select "Publish"
3. Choose "Microsoft Store (New)"
4. Follow the wizard

## Required Assets

Before building, ensure you have the following images in the `Assets` folder:
- `StoreLogo.png` (50x50)
- `Square150x150Logo.png` (150x150)
- `Square44x44Logo.png` (44x44)
- `Wide310x150Logo.png` (310x150)
- `SplashScreen.png` (620x300)

## Package.appxmanifest Configuration

### Important Fields to Update:
- **Publisher**: Change `CN=YourPublisherName` to your actual publisher name
- **PublisherDisplayName**: Update with your company/publisher name
- **Version**: Increment this for each store submission

### Capabilities:
- `runFullTrust`: Required for desktop apps
- `internetClient`: For network access
- `picturesLibrary`: For screenshot saving
- `videosLibrary`: For screen recording
- `documentsLibrary`: For file access

## Code Signing

MSIX packages must be digitally signed. You have several options:

### 1. Test Certificate (Development Only)
```powershell
# Create test certificate
New-SelfSignedCertificate -Type Custom -Subject "CN=SharpShotTest" -KeyUsage DigitalSignature -FriendlyName "SharpShot Test Certificate" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# Export to PFX
Export-Certificate -Cert (Get-ChildItem -Path Cert:\CurrentUser\My -FriendlyName "SharpShot Test Certificate") -FilePath "SharpShotTest.cer"
```

### 2. Commercial Certificate
Purchase a code signing certificate from:
- DigiCert
- Sectigo
- GlobalSign
- Comodo

### 3. Microsoft Store Certificate
Use the certificate provided by Microsoft when you register your app.

## Building for Multiple Architectures

To support both x64 and x86:
1. Update `AppxBundlePlatforms` in `.csproj` to `x64;x86`
2. Build for both platforms
3. The bundle will automatically include both architectures

## Store Submission Checklist

- [ ] MSIX bundle created successfully
- [ ] App tested on target devices
- [ ] Privacy policy created
- [ ] App description and screenshots ready
- [ ] Age rating determined
- [ ] Pricing and availability set
- [ ] Code signing certificate valid
- [ ] App passes Store Kit validation

## Troubleshooting

### Common Issues:

1. **MakeAppx not found**
   - Install Windows SDK
   - Ensure PATH includes SDK bin directory

2. **Code signing errors**
   - Check certificate validity
   - Ensure timestamp server accessible

3. **Dependency issues**
   - Verify all NuGet packages are compatible
   - Check Windows version compatibility

4. **Asset missing errors**
   - Ensure all required images exist in Assets folder
   - Check image dimensions match requirements

## Additional Resources

- [MSIX Documentation](https://docs.microsoft.com/en-us/windows/msix/)
- [Microsoft Store Developer Documentation](https://docs.microsoft.com/en-us/windows/uwp/publish/)
- [Windows App SDK](https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/)

## Support

For issues specific to SharpShot MSIX packaging, check the project issues or create a new one.
