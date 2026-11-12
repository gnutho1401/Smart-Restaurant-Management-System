@echo off
cd /d "%~dp0"
echo ==========================================================
echo   SMART RESTAURANT - BUILD AND PUBLISH RELEASE PIPELINE
echo ==========================================================
dotnet publish SmartRestaurant.WPF\SmartRestaurant.WPF.csproj --configuration Release -o "%~dp0publish"
if exist "SmartRestaurant_Schema_and_Seed.sql" (
    copy /y "SmartRestaurant_Schema_and_Seed.sql" "%~dp0publish\" >nul
)
if %errorlevel% equ 0 (
    echo ==========================================================
    echo   PUBLISH THANH CONG!
    echo   File thuc thi: publish\SmartRestaurant.WPF.exe
    echo ==========================================================
)
echo.
pause
