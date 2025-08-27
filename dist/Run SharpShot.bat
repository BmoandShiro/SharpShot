@echo off
echo Starting SharpShot with bundled dependencies...
echo.
echo Bundled components:
if exist "OBS-Studio" echo - OBS Studio
if exist "ffmpeg" echo - FFmpeg
echo.
start "" "SharpShot.exe"
