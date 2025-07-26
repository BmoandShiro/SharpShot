@echo off
echo Building SharpShot...
cd SharpShot
powershell -ExecutionPolicy Bypass -File "build-and-package.ps1"
pause 