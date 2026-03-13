@echo off
setlocal

echo ========================================
echo   SharpShot 3x Build (Portable + MSIX + NSIS)
echo ========================================
echo.
echo This will:
echo   1) Build the portable release bundle (ZIP + folder)
echo   2) Build the MSIX package
echo   3) Build an NSIS installer from the portable bundle
echo.

REM 1) Portable release (Docker-based bundle)
if exist "Build Release.bat" (
  echo [1/3] Building portable release (Build Release.bat)...
  call "Build Release.bat"
) else (
  echo [WARN] Build Release.bat not found. Skipping portable release build.
)

echo.

REM 2) MSIX package (using existing MSIX scripts)
if exist "Build MSIX with SDK.bat" (
  echo [2/3] Building MSIX package (Build MSIX with SDK.bat)...
  call "Build MSIX with SDK.bat"
) else if exist "Build Simple MSIX.bat" (
  echo [2/3] Building MSIX package (Build Simple MSIX.bat)...
  call "Build Simple MSIX.bat"
) else (
  echo [WARN] No MSIX build script found (Build MSIX with SDK.bat / Build Simple MSIX.bat).
  echo        Skipping MSIX build step.
)

echo.

REM 3) NSIS installer (requires NSIS / makensis.exe)
set NSIS_SCRIPT=Installer\SharpShot.nsi
set PORTABLE_DIR=SharpShot-Release-v1.0

if not exist "%NSIS_SCRIPT%" (
  echo [WARN] NSIS script "%NSIS_SCRIPT%" not found. Skipping NSIS build.
  goto done
)

where makensis >nul 2>&1
if errorlevel 1 (
  echo [WARN] NSIS (makensis.exe) not found in PATH. Install NSIS and re-run 3xbuild.bat to build the NSIS installer.
  goto done
)

if not exist "%PORTABLE_DIR%" (
  echo [WARN] Portable release folder "%PORTABLE_DIR%" not found.
  echo        Make sure Build Release.bat completed successfully or update PORTABLE_DIR in 3xbuild.bat and SharpShot.nsi.
  goto done
)

echo [3/3] Building NSIS installer from "%PORTABLE_DIR%"...
makensis "%NSIS_SCRIPT%"

echo.
echo ========================================
echo   3x build complete (see output above)
echo ========================================

:done
endlocal
pause

