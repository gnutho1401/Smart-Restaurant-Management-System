<#
.SYNOPSIS
    Kích hoạt máy chủ và nạp mô hình Mistral-7B cho phiên làm việc mới của SmartRestaurant.
.DESCRIPTION
    Script tự động:
    1. Kiểm tra và khởi động Local Server LM Studio (nếu chưa chạy).
    2. Nạp mô hình mistral-7b-instruct-v0.3 vào bộ nhớ RAM/VRAM (nếu chưa nạp).
    3. Gửi prompt ping kiểm tra và báo trạng thái sẵn sàng.
#>

param(
    [string]$ModelName = "mistral-7b-instruct-v0.3"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   KÍCH HOẠT MÔ HÌNH MISTRAL-7B CHO PHIÊN LÀM VIỆC MỚI    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Tìm đường dẫn lms.exe
$lmsPath = "$env:USERPROFILE\.lmstudio\bin\lms.exe"
$lmsCmd = Get-Command "lms" -ErrorAction SilentlyContinue
$lmsExe = if ($lmsCmd) { $lmsCmd.Source } elseif (Test-Path $lmsPath) { $lmsPath } else { $null }

if (-not $lmsExe) {
    Write-Warning "Không tìm thấy công cụ lms.exe tại $lmsPath."
    Write-Host "Hướng dẫn: Vui lòng mở ứng dụng LM Studio và bấm chọn model trên thanh trên cùng." -ForegroundColor Yellow
    exit 1
}

# 2. Kiểm tra Local Server
Write-Host "[1/3] Kiểm tra trạng thái máy chủ Local Server..." -ForegroundColor Yellow
$statusOutput = & $lmsExe status 2>&1
$serverRunning = ($statusOutput -match "Server:\s+ON")

if (-not $serverRunning) {
    Write-Host "  -> Máy chủ đang tắt. Đang khởi động Local Server..." -ForegroundColor Yellow
    & $lmsExe server start --cors
    Start-Sleep -Seconds 2
} else {
    Write-Host "  -> Máy chủ Local Server đang chạy." -ForegroundColor Green
}

# 3. Kiểm tra mô hình trong bộ nhớ
Write-Host ""
Write-Host "[2/3] Kiểm tra mô hình trong bộ nhớ RAM/VRAM..." -ForegroundColor Yellow
$statusOutput = & $lmsExe status 2>&1
$modelLoaded = ($statusOutput -match $ModelName)

if ($modelLoaded) {
    Write-Host "  -> Mô hình $ModelName ĐÃ ĐƯỢC NẠP SẴN SÀNG trong bộ nhớ!" -ForegroundColor Green
} else {
    Write-Host "  -> Mô hình chưa nạp. Đang nạp $ModelName vào RAM (khoảng 15-20 giây)..." -ForegroundColor Cyan
    & $lmsExe load $ModelName -y
    Write-Host "  -> Nạp mô hình hoàn tất!" -ForegroundColor Green
}

# 4. Kiểm tra phản hồi thực tế từ AI
Write-Host ""
Write-Host "[3/3] Kiểm tra phản hồi thực tế từ AI..." -ForegroundColor Yellow
& powershell -ExecutionPolicy Bypass -File .\scripts\setup_lmstudio.ps1

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   MÔ HÌNH ĐÃ SẴN SÀNG! BẠN CÓ THỂ MỞ SMARTRESTAURANT     " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

