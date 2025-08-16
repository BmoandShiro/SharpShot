@echo off
echo ========================================
echo    SharpShot MSIX Docker Builder
echo ========================================
echo.
echo This will build the MSIX package using
echo your working Docker environment to avoid
echo local .NET SDK issues.
echo.
echo The MSIX package will include:
echo - SharpShot.exe with all dependencies
echo - OBS Studio and FFmpeg
echo - Ready for Microsoft Store submission
echo.
powershell -ExecutionPolicy Bypass -File "build-msix-docker.ps1"
pause
