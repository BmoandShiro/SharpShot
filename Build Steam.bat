@echo off
setlocal
cd /d "%~dp0"
echo Building SharpShot Steam depot (no OBS, no GitHub updater)...
powershell -ExecutionPolicy Bypass -File "build-steam.ps1"
echo.
pause
endlocal
