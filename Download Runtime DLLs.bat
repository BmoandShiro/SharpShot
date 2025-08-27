@echo off
title Download Runtime DLLs
echo ========================================
echo    Download Runtime DLLs for SharpShot
echo ========================================
echo.
echo This will download ALL 4 essential Visual C++ runtime DLLs
echo and bundle them with your SharpShot application.
echo.
echo DLLs that will be downloaded:
echo - MSVCP140.dll
echo - VCRUNTIME140.dll
echo - VCRUNTIME140_1.dll
echo - MSVCP140_1.dll
echo.
echo Benefits:
echo - Users won't need to install Visual C++ Redistributable
echo - OBS Studio and FFmpeg will work immediately
echo - No more DLL "not found" errors
echo.
echo Press any key to continue...
pause >nul

echo.
echo Running DLL downloader...
powershell -ExecutionPolicy Bypass -File "download-runtime-dlls.ps1"

echo.
echo ========================================
echo Download complete!
echo ========================================
echo.
echo If successful, you can now build SharpShot:
echo   dotnet build --configuration Release
echo.
echo The runtime DLLs will be automatically included.
echo.
echo Press any key to exit...
pause >nul
