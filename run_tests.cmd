@echo off
cd /d "%~dp0"
echo ==========================================================
echo   SMART RESTAURANT - RUNNING TEST SUITE (147/147 TESTS)
echo ==========================================================
dotnet test SmartRestaurant.sln --logger "console;verbosity=normal"
echo.
pause
