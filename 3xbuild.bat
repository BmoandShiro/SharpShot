@echo off
setlocal

echo ========================================
echo   SharpShot 3x Build - Portable, MSI, NSIS
echo ========================================
echo.
echo This will build:
echo   1. Portable release bundle - ZIP and folder
echo   2. MSI installer - WiX Toolset
echo   3. NSIS installer - from portable folder
echo.

REM --- Step 1: Portable (Docker + copy + ZIP) ---
echo [1/3] Building portable release...
if not exist "build-release.ps1" (
  echo ERROR: build-release.ps1 not found.
  goto :done
)
powershell -ExecutionPolicy Bypass -File "build-release.ps1" -NoPrompt
if errorlevel 1 (
  echo Portable build reported an error.
  goto :done
)
echo.

REM --- Step 2: MSI (WiX) ---
echo [2/3] Building MSI installer...
if not exist "build-msi.ps1" (
  echo WARN: build-msi.ps1 not found. Skipping MSI.
  goto :step3
)
powershell -ExecutionPolicy Bypass -File "build-msi.ps1"
if errorlevel 1 (
  echo WARN: MSI build failed or WiX not in PATH. Skipping.
)
:step3
echo.

REM --- Step 3: NSIS ---
echo [3/3] Building NSIS installer...
set "NSIS_SCRIPT=Installer\SharpShot.nsi"
set "PORTABLE_DIR=SharpShot-Release-v1.2.9.5"

if not exist "%NSIS_SCRIPT%" (
  echo WARN: NSIS script not found. Skipping NSIS.
  goto :done
)
set "MAKENSIS="
where makensis >nul 2>&1
if not errorlevel 1 (
  set "MAKENSIS=makensis"
) else (
  if exist "C:\Program Files (x86)\NSIS\makensis.exe" set "MAKENSIS=C:\Program Files (x86)\NSIS\makensis.exe"
  if exist "C:\Program Files\NSIS\makensis.exe" set "MAKENSIS=C:\Program Files\NSIS\makensis.exe"
)
if not defined MAKENSIS (
  echo WARN: makensis not found. Install NSIS from https://nsis.sourceforge.io/ and add to PATH or install to Program Files. Skipping NSIS.
  goto :done
)
if not "%MAKENSIS%"=="makensis" if not exist "%MAKENSIS%" (
  echo WARN: makensis path not found. Skipping NSIS.
  goto :done
)
if not exist "%PORTABLE_DIR%" (
  echo WARN: Portable folder "%PORTABLE_DIR%" not found. Run step 1 first. Skipping NSIS.
  goto :done
)
"%MAKENSIS%" "%NSIS_SCRIPT%"
if errorlevel 1 (
  echo NSIS build reported an error.
)

:done
echo.
echo ========================================
echo   3x build finished. See output above.
echo ========================================
endlocal
pause
