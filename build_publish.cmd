@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo ==========================================================
echo   SMART RESTAURANT - BUILD AND PUBLISH RELEASE PIPELINE
echo ==========================================================

REM 1. Kiem tra va tu dong dong tien trinh WPF neu dang chay de tranh khoa file (MSB3021 / MSB3027)
tasklist /FI "IMAGENAME eq SmartRestaurant.WPF.exe" 2>NUL | find /I /N "SmartRestaurant.WPF.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [THONG BAO] Phat hien SmartRestaurant.WPF.exe dang chay.
    echo Dang dong tien trinh de mo khoa thu muc publish...
    taskkill /F /IM SmartRestaurant.WPF.exe >nul 2>&1
    ping -n 2 127.0.0.1 >nul
    echo Da giai phong file khoa thanh cong.
    echo.
)

REM 2. Thuc thi lenh dotnet publish
echo Dang bien dich va xuat ban phien ban Release...
dotnet publish SmartRestaurant.WPF\SmartRestaurant.WPF.csproj --configuration Release -o "%~dp0publish"
set "PUB_ERR=%errorlevel%"

if %PUB_ERR% neq 0 (
    echo.
    echo ==========================================================
    echo   [LOI] PUBLISH THAT BAI! MA LOI: %PUB_ERR%
    echo   Vui long kiem tra lai ma nguon hoac quyen ghi file.
    echo ==========================================================
    pause
    exit /b %PUB_ERR%
)

REM 3. Sao chep tap tin CSDL mau vao thu muc xuat ban
if exist "SmartRestaurant_Schema_and_Seed.sql" (
    copy /y "SmartRestaurant_Schema_and_Seed.sql" "%~dp0publish\" >nul
)

echo.
echo ==========================================================
echo   PUBLISH THANH CONG!
echo   Thu muc: %~dp0publish
echo   Tap tin thuc thi: publish\SmartRestaurant.WPF.exe
echo ==========================================================
echo.
pause
