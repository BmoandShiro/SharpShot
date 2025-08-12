@echo off
echo Starting SharpShot Quick Build...
echo This will build SharpShot and bundle FFmpeg + OBS Studio
echo.
cd SharpShot
echo Starting quick build...
echo.
powershell -ExecutionPolicy Bypass -File "build-bundle-complete.ps1"
echo.
echo Build complete! Press any key to close...
pause > nul
