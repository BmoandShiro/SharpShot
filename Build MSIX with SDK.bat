@echo off
echo Building SharpShot MSIX using Windows SDK...
echo.

REM Change to the script directory
cd /d "%~dp0"

REM Run the PowerShell script
powershell -ExecutionPolicy Bypass -File "build-with-sdk.ps1" -Configuration Release -Clean

echo.
echo Build completed! Check the bin\Release folder for output files.
pause
