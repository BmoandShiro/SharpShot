@echo off
echo Creating SharpShot Distribution Package...
cd SharpShot
powershell -ExecutionPolicy Bypass -File "create-distribution.ps1"
pause 