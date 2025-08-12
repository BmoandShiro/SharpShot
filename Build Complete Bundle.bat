@echo off
echo Starting SharpShot Complete Bundle Build...
echo This will build SharpShot and bundle both FFmpeg and OBS Studio
echo.
echo Clearing Docker cache and containers...
docker system prune -f
docker compose -f SharpShot/docker-compose.dev.yml down
docker compose -f SharpShot/docker-compose.dev.yml rm -f
echo Docker environment cleaned!
cd SharpShot
echo.
echo Starting complete bundle build with Docker...
echo.
powershell -ExecutionPolicy Bypass -File "build-bundle-complete.ps1" -CreateZip -LaunchAfterBuild
echo.
echo Press any key to close this window...
pause > nul
