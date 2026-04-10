@echo off
setlocal

echo ========================================
echo   SharpShot NSIS Builder
echo ========================================
echo.
echo This will:
echo   - Use the portable folder:
echo       SharpShot-Release-v1.2.9.4
echo   - Build SharpShot-Setup.exe with NSIS
echo.

set "NSIS_SCRIPT=Installer\SharpShot.nsi"
set "PORTABLE_DIR=SharpShot-Release-v1.2.9.4"

if not exist "%PORTABLE_DIR%" (
  echo [ERROR] Portable folder "%PORTABLE_DIR%" not found.
  echo         Run the portable build (3xbuild or Build Release) first.
  goto :end
)

if not exist "%NSIS_SCRIPT%" (
  echo [ERROR] NSIS script "%NSIS_SCRIPT%" not found.
  goto :end
)

REM Try to find makensis (PATH or common install locations)
set "MAKENSIS="
where makensis >nul 2>&1
if not errorlevel 1 (
  set "MAKENSIS=makensis"
) else (
  if exist "C:\Program Files (x86)\NSIS\makensis.exe" set "MAKENSIS=C:\Program Files (x86)\NSIS\makensis.exe"
  if exist "C:\Program Files\NSIS\makensis.exe" set "MAKENSIS=C:\Program Files\NSIS\makensis.exe"
)

if not defined MAKENSIS (
  echo [ERROR] makensis not found.
  echo         Install NSIS from https://nsis.sourceforge.io/ and
  echo         either add it to PATH or install to Program Files.
  goto :end
)

if not "%MAKENSIS%"=="makensis" if not exist "%MAKENSIS%" (
  echo [ERROR] makensis path "%MAKENSIS%" not found.
  goto :end
)

echo.
echo [INFO] Using makensis: %MAKENSIS%
echo [INFO] Building NSIS installer...
"%MAKENSIS%" "%NSIS_SCRIPT%"
if errorlevel 1 (
  echo.
  echo [ERROR] NSIS build failed. See output above.
) else (
  echo.
  echo [OK] NSIS build completed successfully.
  echo     Output: SharpShot-Setup.exe
)

:end
echo.
echo Press any key to close this window...
pause >nul
endlocal
