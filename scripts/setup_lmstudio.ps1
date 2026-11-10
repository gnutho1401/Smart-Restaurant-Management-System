<#
.SYNOPSIS
    Script kiem tra va thiet lap moi truong Local GenAI cho SmartRestaurant.
#>

param(
    [int]$Port = 1234,
    [string]$ModelName = "mistral-7b-instruct-v0.3"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   SMARTRESTAURANT - KIEM TRA KET NOI LOCAL GENAI         " -ForegroundColor Cyan
Write-Host "   Mo hinh muc tieu: Mistral-7B-Instruct                  " -ForegroundColor Cyan
Write-Host "   Endpoint mac dinh: http://localhost:1234 hoac 1235     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Kiem tra phan cung
Write-Host "[1/4] Kiem tra tai nguyen phan cung..." -ForegroundColor Yellow
$ram = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 1)
Write-Host "  -> Bo nho RAM vat ly: $ram GB" -ForegroundColor Green
if ($ram -lt 8.0) {
    Write-Warning "  Canh bao: RAM he thong duoi 8GB. Can khoang 4.5GB RAM de chay Mistral-7B."
} else {
    Write-Host "  -> Dat yeu cau phan cung cho mo hinh 7B Q4_K_M." -ForegroundColor Green
}

# 2. Kiem tra LM Studio CLI / Ung dung
Write-Host ""
Write-Host "[2/4] Kiem tra cong cu LM Studio..." -ForegroundColor Yellow
$lmsPath = "$env:USERPROFILE\.lmstudio\bin\lms.exe"
$lmsCmd = Get-Command "lms" -ErrorAction SilentlyContinue
if (-not $lmsCmd -and (Test-Path $lmsPath)) {
    $lmsCmd = [PSCustomObject]@{ Source = $lmsPath }
}

if ($lmsCmd) {
    Write-Host "  -> Da tim thay LM Studio CLI: $($lmsCmd.Source)" -ForegroundColor Green
    try {
        $models = & $lmsCmd.Source ls 2>$null
        Write-Host "  -> Danh sach mo hinh hien co:" -ForegroundColor Gray
        $models | ForEach-Object { Write-Host "     $_" -ForegroundColor DarkGray }
    } catch {}
} else {
    Write-Host "  -> LM Studio CLI chua duoc them vao PATH." -ForegroundColor Gray
}

# 3. Kiem tra ket noi cong Localhost (1234 hoac 1235)
Write-Host ""
Write-Host "[3/4] Kiem tra cong lang nghe localhost (1234 / 1235)..." -ForegroundColor Yellow
$activePort = $Port
$tcpTest = Test-NetConnection -ComputerName localhost -Port $Port -WarningAction SilentlyContinue
if (-not $tcpTest.TcpTestSucceeded) {
    $tcpTest2 = Test-NetConnection -ComputerName localhost -Port 1235 -WarningAction SilentlyContinue
    if ($tcpTest2.TcpTestSucceeded) {
        $activePort = 1235
        $tcpTest = $tcpTest2
    }
}

if ($tcpTest.TcpTestSucceeded) {
    Write-Host "  -> Cong $activePort DANG HOAT DONG va san sang ket noi!" -ForegroundColor Green
} else {
    Write-Warning "  -> Cong 1234 va 1235 deu CHUA MO."
    Write-Host "  -> Ban co the khoi dong LM Studio hoac dung script: .\start_ai.bat" -ForegroundColor Cyan
}

# 4. Kiem tra gui API chat completion thu nghiem
if ($tcpTest.TcpTestSucceeded) {
    Write-Host ""
    Write-Host "[4/4] Gui prompt thu nghiem Context Grounding den LM Studio (port $activePort)..." -ForegroundColor Yellow
    try {
        $modelsResponse = Invoke-RestMethod -Uri "http://localhost:$activePort/v1/models" -Method Get -TimeoutSec 5 -ErrorAction SilentlyContinue
        $detectedModel = $ModelName
        if ($modelsResponse -and $modelsResponse.data -and $modelsResponse.data.Count -gt 0) {
            $detectedModel = $modelsResponse.data[0].id
            Write-Host "  -> Da nhan dien model dang chay: $detectedModel" -ForegroundColor Cyan
        }

        $payload = @{
            model = $detectedModel
            temperature = 0.3
            messages = @(
                @{
                    role = "system"
                    content = "Ban la tro ly ao SmartRestaurant. Hay xac nhan ket noi thanh cong trong 1 cau ngan gon."
                },
                @{
                    role = "user"
                    content = "Ping ket noi he thong."
                }
            )
        } | ConvertTo-Json -Depth 5

        $response = Invoke-RestMethod -Uri "http://localhost:$activePort/v1/chat/completions" -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 30
        $answer = $response.choices[0].message.content
        Write-Host "  -> AI Phan hoi thanh cong:" -ForegroundColor Green
        Write-Host "     `"$answer`"" -ForegroundColor Cyan
    } catch {
        Write-Warning "  -> Khong gui duoc yeu cau: $_"
    }
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Thiet lap hoan tat. He thong san sang cho module AI!" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
