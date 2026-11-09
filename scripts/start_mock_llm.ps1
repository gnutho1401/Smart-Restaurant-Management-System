<#
.SYNOPSIS
    Lightweight Mock OpenAI / LM Studio Local Server cho kiểm thử phát triển và CI/CD.
.DESCRIPTION
    Khởi tạo một HTTP Listener tại http://localhost:1234/v1/chat/completions
    Mô phỏng phản hồi từ Mistral-7B-Instruct tương thích với SmartRestaurant.AIEngine.LmStudioService.
#>

param(
    [int]$Port = 1234,
    [int]$TimeoutSeconds = 0 # 0 = chạy liên tục đến khi dừng bằng Ctrl+C
)

$listener = New-Object System.Net.HttpListener
$prefix = "http://localhost:$Port/"
$listener.Prefixes.Add($prefix)

try {
    $listener.Start()
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "   MOCK LM STUDIO SERVER (PORT $Port) ĐANG CHẠY...        " -ForegroundColor Green
    Write-Host "   Endpoint: http://localhost:$Port/v1/chat/completions   " -ForegroundColor Cyan
    Write-Host "   Nhấn Ctrl+C để dừng server.                            " -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Green

    $startTime = [DateTime]::UtcNow

    while ($listener.IsListening) {
        if ($TimeoutSeconds -gt 0) {
            $elapsed = ([DateTime]::UtcNow - $startTime).TotalSeconds
            if ($elapsed -ge $TimeoutSeconds) {
                Write-Host "Đã hết thời gian chạy ($TimeoutSeconds s). Tự động dừng server." -ForegroundColor Yellow
                break
            }
        }

        # Bắt request bất đồng bộ kèm timeout ngắn để có thể thoát vòng lặp
        $contextTask = $listener.GetContextAsync()
        while (-not $contextTask.AsyncWaitHandle.WaitOne(500)) {
            if ($TimeoutSeconds -gt 0 -and ([DateTime]::UtcNow - $startTime).TotalSeconds -ge $TimeoutSeconds) {
                break
            }
        }

        if (-not $contextTask.IsCompleted) {
            continue
        }

        $context = $contextTask.Result
        $request = $context.Request
        $response = $context.Response

        # Cho phép CORS
        $response.Headers.Add("Access-Control-Allow-Origin", "*")
        $response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization")
        $response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS")

        if ($request.HttpMethod -eq "OPTIONS") {
            $response.StatusCode = 200
            $response.Close()
            continue
        }

        $urlPath = $request.Url.AbsolutePath
        Write-Host "[$([DateTime]::Now.ToString('HH:mm:ss'))] Nhận $($request.HttpMethod) $urlPath" -ForegroundColor Gray

        if ($request.HttpMethod -eq "GET" -and ($urlPath -eq "/v1/models" -or $urlPath -eq "/")) {
            $modelsPayload = @{
                object = "list"
                data = @(
                    @{
                        id = "mistral-7b-instruct-v0.2.Q4_K_M.gguf"
                        object = "model"
                        owned_by = "local"
                    }
                )
            } | ConvertTo-Json
            $buffer = [System.Text.Encoding]::UTF8.GetBytes($modelsPayload)
            $response.ContentType = "application/json; charset=utf-8"
            $response.ContentLength64 = $buffer.Length
            $response.OutputStream.Write($buffer, 0, $buffer.Length)
            $response.Close()
            continue
        }

        if ($request.HttpMethod -eq "POST" -and $urlPath -eq "/v1/chat/completions") {
            $reader = New-Object System.IO.StreamReader($request.InputStream, $request.ContentEncoding)
            $bodyText = $reader.ReadToEnd()
            $reader.Close()

            $parsed = $null
            try {
                $parsed = $bodyText | ConvertFrom-Json
            } catch {}

            $userPrompt = ""
            $systemPrompt = ""
            if ($parsed -and $parsed.messages) {
                foreach ($msg in $parsed.messages) {
                    if ($msg.role -eq "user") { $userPrompt += " " + $msg.content }
                    if ($msg.role -eq "system") { $systemPrompt += " " + $msg.content }
                }
            }

            $aiReply = "Xin chào! Tôi là trợ lý ẩm thực AI của SmartRestaurant. Tôi có thể hỗ trợ quý khách gọi món, tư vấn dị ứng hoặc phân tích hiệu quả vận hành nhà hàng."

            if ($userPrompt -match "món|thực đơn|gợi ý|recommend|ăn gì|chay|hải sản") {
                $aiReply = "Dựa trên thực đơn hôm nay và sở thích của bạn, tôi xin gợi ý:`n1. Phở Bò Tái Nạm (Best Seller) - Nước dùng ninh 12 tiếng thơm ngọt đậm đà.`n2. Cơm Tấm Sườn Bì Chả - Thơm ngon chuẩn vị truyền thống.`n3. Trà Đào Cam Sả - Giải nhiệt thanh mát.`nChúc quý khách có trải nghiệm ẩm thực trọn vẹn!"
            } elseif ($userPrompt -match "mô tả|description|viết|giới thiệu món") {
                $aiReply = "Hòa quyện tinh hoa ẩm thực truyền thống, món ăn mang hương vị đậm đà khó cưỡng với nguyên liệu tươi sạch được tuyển chọn mỗi sáng, chế biến tinh tế mang lại trải nghiệm thị giác và vị giác tuyệt hảo cho thực khách."
            } elseif ($userPrompt -match "doanh thu|revenue|báo cáo|tài chính|trend") {
                $aiReply = "Phân tích doanh thu SmartRestaurant:`n- Doanh thu ổn định với tỷ trọng giờ cao điểm 18h-20h chiếm hơn 45% tổng doanh số.`n- Xu hướng tích cực từ nhóm món nước và đồ uống kèm.`n- Đề xuất: Đẩy mạnh combo trưa để tối ưu doanh thu các khung giờ thấp điểm."
            } elseif ($userPrompt -match "BCG|Stars|Plowhorses|Puzzles|Dogs|ma trận") {
                $aiReply = "Phân tích Menu Matrix (BCG):`n- Stars: Phở Bò Tái, Trà Đào (Biên lợi nhuận cao, doanh số mạnh -> Giữ vững chất lượng).`n- Plowhorses: Cơm Tấm (Doanh số cao, lợi nhuận trung bình -> Tối ưu chi phí nguyên liệu).`n- Puzzles: Lẩu Thái Hải Sản (Lợi nhuận cao nhưng doanh số khiêm tốn -> Tăng quảng bá).`n- Dogs: Salad Rau Củ (Cân nhắc tinh gọn thực đơn)."
            } elseif ($userPrompt -match "dự báo|nhập hàng|tồn kho|reorder|kho") {
                $aiReply = "Dự báo nguyên vật liệu tuần tới:`n- Thịt bò tươi: Dự báo tiêu thụ 35kg (Cần bổ sung thêm 15kg trước thứ 6).`n- Gạo ST25: Tồn kho dồi dào, đủ cho 10 ngày vận hành.`n- Rau thơm, hành lá: Tốc độ tiêu thụ nhanh, duy trì nhập hằng ngày để đảm bảo độ tươi."
            } elseif ($userPrompt -match "Ping|ping|kết nối") {
                $aiReply = "Kết nối thành công tới mô hình Mistral-7B-Instruct (SmartRestaurant Local AI Engine)."
            }

            $responseObj = @{
                id = "chatcmpl-" + [System.Guid]::NewGuid().ToString("N")
                object = "chat.completion"
                created = [int][DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
                model = if ($parsed -and $parsed.model) { $parsed.model } else { "mistral-7b-instruct-v0.2.Q4_K_M.gguf" }
                choices = @(
                    @{
                        index = 0
                        message = @{
                            role = "assistant"
                            content = $aiReply
                        }
                        finish_reason = "stop"
                    }
                )
                usage = @{
                    prompt_tokens = 50
                    completion_tokens = 100
                    total_tokens = 150
                }
            }

            $jsonResponse = $responseObj | ConvertTo-Json -Depth 5
            $buffer = [System.Text.Encoding]::UTF8.GetBytes($jsonResponse)
            $response.ContentType = "application/json; charset=utf-8"
            $response.ContentLength64 = $buffer.Length
            $response.OutputStream.Write($buffer, 0, $buffer.Length)
            $response.Close()
            continue
        }

        # Fallback 404
        $response.StatusCode = 404
        $response.Close()
    }
}
finally {
    if ($listener -and $listener.IsListening) {
        $listener.Stop()
        $listener.Close()
    }
    Write-Host "Mock LM Studio Server đã dừng." -ForegroundColor Yellow
}

