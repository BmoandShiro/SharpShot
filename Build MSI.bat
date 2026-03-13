@echo off
setlocal

echo ========================================
echo   SharpShot MSI Builder
echo ========================================
echo.
echo This will:
echo   - Use the existing portable release folder
echo     SharpShot-Release-v1.2.8.3
echo   - Harvest files with WiX
echo   - Build SharpShot-1.2.8.3.msi in bin\Release
echo.

if not exist "build-msi.ps1" (
  echo [ERROR] build-msi.ps1 not found in this folder.
  echo         Make sure you run this from the SharpShot project directory.
  goto :end
)

powershell -ExecutionPolicy Bypass -File "build-msi.ps1"
if errorlevel 1 (
  echo.
  echo [ERROR] MSI build failed. See messages above.
) else (
  echo.
  echo [OK] MSI build completed successfully.
  echo     Check bin\Release\SharpShot-1.2.8.3.msi
)

:end
echo.
echo Press any key to close this window...
pause >nul
endlocal
