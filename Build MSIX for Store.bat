@echo off
echo Building SharpShot MSIX Bundle for Microsoft Store...
echo.

REM Change to the script directory
cd /d "%~dp0"

REM Run the PowerShell script
powershell -ExecutionPolicy Bypass -File "build-msix.ps1" -Configuration Release -Platform x64

echo.
echo Build completed! Check the bin\Release folder for output files.
pause
