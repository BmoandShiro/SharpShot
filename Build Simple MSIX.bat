@echo off
echo Building SharpShot MSIX package...
powershell -ExecutionPolicy Bypass -File "build-simple-msix.ps1"
pause
