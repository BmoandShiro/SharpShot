@echo off
cd /d "%~dp0"
echo Stops any running SharpShot, then starts dotnet watch...
powershell -ExecutionPolicy Bypass -File "Run SharpShot.ps1" -StopExisting
pause
