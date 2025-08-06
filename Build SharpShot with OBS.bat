@echo off
echo ========================================
echo SharpShot Build with OBS Studio Bundling
echo ========================================
echo.

echo Building SharpShot with bundled OBS Studio...
echo This will create a complete distribution package.
echo.

powershell -ExecutionPolicy Bypass -File "build-with-obs.ps1" -Configuration Release -Platform x64

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo Build completed successfully!
    echo ========================================
    echo.
    echo The distribution package has been created.
    echo Look for: SharpShot-with-OBS-Release.zip
    echo.
    pause
) else (
    echo.
    echo ========================================
    echo Build failed!
    echo ========================================
    echo.
    echo Check the error messages above.
    echo.
    pause
) 