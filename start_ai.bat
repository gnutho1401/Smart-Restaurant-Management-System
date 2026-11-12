@echo off
chcp 65001 > nul
cd /d "%~dp0"
title Kich Hoat Mo Hinh Local AI (Mistral-7B) - SmartRestaurant
echo ==========================================================
echo   KICH HOAT MO HINH MISTRAL-7B CHO PHIEN LAM VIEC MOI
echo ==========================================================
powershell -ExecutionPolicy Bypass -File .\scripts\start_ai.ps1
echo.
pause
