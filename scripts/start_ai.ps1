<#
.SYNOPSIS
    Kich hoat may chu va nap mo hinh Mistral-7B cho phien lam viec moi cua SmartRestaurant.
#>

param(
    [string]$ModelName = "mistral-7b-instruct-v0.3"
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   KICH HOAT MO HINH MISTRAL-7B CHO PHIEN LAM VIEC MOI    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Kiem tra xem cong 1234 hoac 1235 da co server nao lang nghe chua
$portActive = $false
try {
    $client = New-Object System.Net.Sockets.TcpClient
    $iar = $client.BeginConnect("127.0.0.1", 1234, $null, $null)
    $success = $iar.AsyncWaitHandle.WaitOne(1000)
    if ($success -and $client.Connected) {
        $portActive = $true
        $client.EndConnect($iar)
    }
    $client.Close()
} catch {}

if ($portActive) {
    Write-Host "[1/3] May chu Local AI da dang hoat dong tren cong 1234!" -ForegroundColor Green
} else {
    Write-Host "[1/3] Kiem tra cong cu LM Studio CLI..." -ForegroundColor Yellow
    $lmsPath = "$env:USERPROFILE\.lmstudio\bin\lms.exe"
    $lmsCmd = Get-Command "lms" -ErrorAction SilentlyContinue
    $lmsExe = if ($lmsCmd) { $lmsCmd.Source } elseif (Test-Path $lmsPath -ErrorAction SilentlyContinue) { $lmsPath } else { $null }

    if ($lmsExe) {
        Write-Host "  -> Tim thay LM Studio CLI tai: $lmsExe" -ForegroundColor Green
        $statusOutput = & $lmsExe status 2>&1
        $serverRunning = ($statusOutput -match "Server:\s+ON")

        if (-not $serverRunning) {
            Write-Host "  -> Dang khoi dong Local Server..." -ForegroundColor Yellow
            & $lmsExe server start --cors
            Start-Sleep -Seconds 2
        } else {
            Write-Host "  -> May chu Local Server dang chay." -ForegroundColor Green
        }

        # Kiem tra mo hinh
        Write-Host ""
        Write-Host "[2/3] Kiem tra mo hinh trong bo nho RAM/VRAM..." -ForegroundColor Yellow
        $statusOutput = & $lmsExe status 2>&1
        $modelLoaded = ($statusOutput -match $ModelName)

        if ($modelLoaded) {
            Write-Host "  -> Mo hinh $ModelName DA DUOC NAP SAN SANG!" -ForegroundColor Green
        } else {
            Write-Host "  -> Dang nap $ModelName vao RAM (khoang 15-20 giay)..." -ForegroundColor Cyan
            & $lmsExe load $ModelName -y
            Write-Host "  -> Nap mo hinh hoan tat!" -ForegroundColor Green
        }
    } else {
        Write-Warning "Khong tim thay lms.exe CLI tren may."
        Write-Host "  -> Neu ban da mo LM Studio GUI, vui long kiem tra Local Server tren cong 1234." -ForegroundColor Yellow
        Write-Host "  -> Dang khoi dong May chu AI noi bo (Mock LLM Server) tai cong 1234 de ban test ngay..." -ForegroundColor Cyan
        & powershell -ExecutionPolicy Bypass -File .\scripts\start_mock_llm.ps1
        exit 0
    }
}

# Kiem tra phan hoi thuc te
Write-Host ""
Write-Host "[3/3] Kiem tra phan hoi thuc te tu AI..." -ForegroundColor Yellow
if (Test-Path .\scripts\setup_lmstudio.ps1) {
    & powershell -ExecutionPolicy Bypass -File .\scripts\setup_lmstudio.ps1
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   MO HINH DA SAN SANG! BAN CO THE MO SMARTRESTAURANT     " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green