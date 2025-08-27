@echo off
echo ========================================
echo    SharpShot Release Builder
echo ========================================
echo.
echo This will create a proper release package
echo using your working OBS Docker workflow.
echo.
echo The release package will include:
echo - SharpShot.exe (standalone)
echo - Complete OBS Studio installation
echo - FFmpeg (if available)
echo - Working launchers
echo.
powershell -ExecutionPolicy Bypass -File "build-release.ps1"
pause
