@echo off
chcp 65001 > nul
title Kích Hoạt Mô Hình Local AI (Mistral-7B) - SmartRestaurant
echo ==========================================================
echo   KÍCH HOẠT MÔ HÌNH MISTRAL-7B CHO PHIÊN LÀM VIỆC MỚI
echo ==========================================================
powershell -ExecutionPolicy Bypass -File .\scripts\start_ai.ps1
echo.
pause

