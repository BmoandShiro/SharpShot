@echo off
title Find Runtime DLLs
echo ========================================
echo    Find Runtime DLLs for SharpShot
echo ========================================
echo.
echo This will search your system for the Visual C++ runtime DLLs
echo and show you exactly where to copy them from.
echo.
echo DLLs we're looking for:
echo - MSVCP140.dll
echo - VCRUNTIME140.dll
echo - VCRUNTIME140_1.dll
echo - MSVCP140_1.dll
echo.
echo Press any key to start searching...
pause >nul

echo.
echo Searching for runtime DLLs...
powershell -ExecutionPolicy Bypass -File "find-runtime-dlls.ps1"

echo.
echo ========================================
echo Search complete!
echo ========================================
echo.
echo Follow the instructions above to copy the DLLs.
echo.
echo Press any key to exit...
pause >nul
