<#
.SYNOPSIS
    Script kiểm tra và thiết lập môi trường Local Generative AI (LM Studio & Mistral-7B) cho SmartRestaurant.
.DESCRIPTION
    Dự án PRN212 Nhóm 6 sử dụng LM Studio chạy cục bộ tại http://localhost:1234/v1 với mô hình Mistral-7B Q4_K_M.
    Script này kiểm tra môi trường, cổng 1234 và gửi kiểm thử prompt Context Grounding.
#>

param(
    [int]$Port = 1234,
    [string]$ModelName = "mistral-7b-instruct-v0.2.Q4_K_M.gguf"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   SMARTRESTAURANT - THIẾT LẬP MÔI TRƯỜNG LOCAL GENAI     " -ForegroundColor Cyan
Write-Host "   Mô hình: Mistral-7B-Instruct (Quantized 4-bit GGUF)    " -ForegroundColor Cyan
Write-Host "   Endpoint: http://localhost:$Port/v1                    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Kiểm tra phần cứng
Write-Host "[1/4] Kiểm tra tài nguyên phần cứng..." -ForegroundColor Yellow
$ram = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 1)
Write-Host "  -> Bộ nhớ RAM vật lý: $ram GB" -ForegroundColor Green
if ($ram -lt 8.0) {
    Write-Warning "  Cảnh báo: RAM hệ thống dưới 8GB. Mistral-7B 4-bit cần khoảng 4.5GB RAM để suy luận mượt mà."
} else {
    Write-Host "  -> Đạt yêu cầu phần cứng cho mô hình 7B Q4_K_M." -ForegroundColor Green
}

# 2. Kiểm tra LM Studio CLI / Ứng dụng
Write-Host ""
Write-Host "[2/4] Kiểm tra công cụ LM Studio..." -ForegroundColor Yellow
$lmsPath = "$env:USERPROFILE\.lmstudio\bin\lms.exe"
$lmsCmd = Get-Command "lms" -ErrorAction SilentlyContinue
if (-not $lmsCmd -and (Test-Path $lmsPath)) {
    $lmsCmd = [PSCustomObject]@{ Source = $lmsPath }
}

if ($lmsCmd) {
    Write-Host "  -> Đã tìm thấy LM Studio CLI: $($lmsCmd.Source)" -ForegroundColor Green
    try {
        $models = & $lmsCmd.Source ls 2>$null
        Write-Host "  -> Danh sách mô hình hiện có:" -ForegroundColor Gray
        $models | ForEach-Object { Write-Host "     $_" -ForegroundColor DarkGray }
    } catch {}
} else {
    Write-Host "  -> LM Studio CLI chưa được thêm vào PATH." -ForegroundColor Gray
    Write-Host "  -> Hướng dẫn cài đặt:" -ForegroundColor White
    Write-Host "     1. Tải ứng dụng LM Studio từ: https://lmstudio.ai" -ForegroundColor White
    Write-Host "     2. Tìm và tải mô hình: 'TheBloke/Mistral-7B-Instruct-v0.2-GGUF' (chọn bản Q4_K_M)" -ForegroundColor White
    Write-Host "     3. Chuyển sang tab 'Local Server' (<-> icon), chọn cổng $Port và bấm 'Start Server'" -ForegroundColor White
}

# 3. Kiểm tra kết nối cổng Localhost (1234 hoặc 1235)
Write-Host ""
Write-Host "[3/4] Kiểm tra cổng lắng nghe localhost (1234 / 1235)..." -ForegroundColor Yellow
$activePort = $Port
$tcpTest = Test-NetConnection -ComputerName localhost -Port $Port -WarningAction SilentlyContinue
if (-not $tcpTest.TcpTestSucceeded) {
    # Thử cổng 1235 (LM Studio 0.3)
    $tcpTest2 = Test-NetConnection -ComputerName localhost -Port 1235 -WarningAction SilentlyContinue
    if ($tcpTest2.TcpTestSucceeded) {
        $activePort = 1235
        $tcpTest = $tcpTest2
    }
}

if ($tcpTest.TcpTestSucceeded) {
    Write-Host "  -> Cổng $activePort ĐANG HOẠT ĐỘNG và sẵn sàng kết nối!" -ForegroundColor Green
} else {
    Write-Warning "  -> Cổng 1234 và 1235 đều CHƯA MỞ."
    Write-Host "  -> Bạn có thể khởi động LM Studio hoặc dùng script chạy Mock Server thử nghiệm:" -ForegroundColor Cyan
    Write-Host "     powershell -ExecutionPolicy Bypass -File .\scripts\start_mock_llm.ps1" -ForegroundColor Yellow
}

# 4. Kiểm tra gửi API chat completion thử nghiệm (nếu server đang chạy)
if ($tcpTest.TcpTestSucceeded) {
    Write-Host ""
    Write-Host "[4/4] Gửi prompt thử nghiệm Context Grounding đến LM Studio (port $activePort)..." -ForegroundColor Yellow
    try {
        # Phát hiện model đang được load trên server
        $modelsResponse = Invoke-RestMethod -Uri "http://localhost:$activePort/v1/models" -Method Get -TimeoutSec 5 -ErrorAction SilentlyContinue
        $detectedModel = $ModelName
        if ($modelsResponse -and $modelsResponse.data -and $modelsResponse.data.Count -gt 0) {
            $detectedModel = $modelsResponse.data[0].id
            Write-Host "  -> Đã nhận diện model đang chạy: $detectedModel" -ForegroundColor Cyan
        }

        $payload = @{
            model = $detectedModel
            temperature = 0.3
            messages = @(
                @{
                    role = "system"
                    content = "Bạn là trợ lý ảo SmartRestaurant. Hãy xác nhận kết nối thành công ngắn gọn trong 1 câu tiếng Việt."
                },
                @{
                    role = "user"
                    content = "Ping kết nối hệ thống."
                }
            )
        } | ConvertTo-Json -Depth 5

        $response = Invoke-RestMethod -Uri "http://localhost:$activePort/v1/chat/completions" -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 15
        $answer = $response.choices[0].message.content
        Write-Host "  -> AI Phản hồi thành công:" -ForegroundColor Green
        Write-Host "     `"$answer`"" -ForegroundColor Cyan
    } catch {
        Write-Warning "  -> Không gửi được yêu cầu: $_"
    }
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Thiết lập hoàn tất. Hệ thống sẵn sàng cho module AI!" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

