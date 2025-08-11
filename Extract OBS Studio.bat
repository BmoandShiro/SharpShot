@echo off
echo ========================================
echo OBS Studio Extraction
echo ========================================
echo.
echo This will download and extract OBS Studio
echo to the SharpShot/OBS-Studio/ directory.
echo.
echo Press any key to continue...
pause >nul

powershell -ExecutionPolicy Bypass -File "extract-obs.ps1"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo OBS Studio extracted successfully!
    echo ========================================
    echo.
    echo You can now build SharpShot with OBS bundling.
    echo.
    pause
) else (
    echo.
    echo ========================================
    echo Extraction failed!
    echo ========================================
    echo.
    echo Check the error messages above.
    echo.
    pause
) 