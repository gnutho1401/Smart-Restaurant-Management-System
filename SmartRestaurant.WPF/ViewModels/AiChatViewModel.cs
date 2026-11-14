using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SmartRestaurant.AIEngine;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// ViewModel driving the conversational Local GenAI Assistant and business intelligence insights.
/// Includes semantic response caching, interactive quick actions, clipboard export, and runtime Cloud AI settings.
/// </summary>
public class AiChatViewModel : ViewModelBase
{
    private readonly IAiService _aiService;
    private readonly AiAnalyticsEngine _analyticsEngine;
    private readonly IInventoryService? _inventoryService;

    private ObservableCollection<ChatMessageViewModel> _messages = new();
    private string _inputQuery = string.Empty;
    private bool _isBusy;
    private string _statusInfo = "Sẵn sàng kết nối LM Studio (localhost:1234 / 1235)";

    // Semantic Response Cache to avoid redundant LLM latency
    private readonly Dictionary<string, (string Content, DateTime CachedAt)> _semanticCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    // AI Settings Overlay State
    private bool _isSettingsOpen;
    private string _geminiApiKeyInput = string.Empty;
    private string _selectedGeminiModel = "gemini-flash-lite-latest";
    private string _settingsFeedback = string.Empty;

    // 1-Click PO Draft State
    private List<PurchaseOrderDraftItem>? _lastPoDraft;
    private bool _isPoDraftAvailable;
    private string _connectionBadgeText = "🔍 Kiểm tra kết nối AI";
    private bool _isConnectionHealthy;

    public ObservableCollection<ChatMessageViewModel> Messages
    {
        get => _messages;
        set => SetProperty(ref _messages, value);
    }

    public string InputQuery
    {
        get => _inputQuery;
        set => SetProperty(ref _inputQuery, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusInfo
    {
        get => _statusInfo;
        set => SetProperty(ref _statusInfo, value);
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    public string ConnectionBadgeText
    {
        get => _connectionBadgeText;
        set => SetProperty(ref _connectionBadgeText, value);
    }

    public bool IsConnectionHealthy
    {
        get => _isConnectionHealthy;
        set => SetProperty(ref _isConnectionHealthy, value);
    }

    public string GeminiApiKeyInput
    {
        get => _geminiApiKeyInput;
        set => SetProperty(ref _geminiApiKeyInput, value);
    }

    public string SelectedGeminiModel
    {
        get => _selectedGeminiModel;
        set => SetProperty(ref _selectedGeminiModel, value);
    }

    public string SettingsFeedback
    {
        get => _settingsFeedback;
        set => SetProperty(ref _settingsFeedback, value);
    }

    public ObservableCollection<string> AvailableGeminiModels { get; } = new()
    {
        "gemini-flash-lite-latest",
        "gemini-1.5-flash",
        "gemini-1.5-pro"
    };

    public bool IsPoDraftAvailable
    {
        get => _isPoDraftAvailable;
        set => SetProperty(ref _isPoDraftAvailable, value);
    }

    public IAsyncRelayCommand SendQueryCommand { get; }
    public IAsyncRelayCommand RunBcgCommand { get; }
    public IAsyncRelayCommand RunPeakHoursCommand { get; }
    public IAsyncRelayCommand RunReorderCommand { get; }
    public IAsyncRelayCommand RunRevenueTrendCommand { get; }
    public IAsyncRelayCommand ApplyAiPoDraftCommand { get; }
    public IAsyncRelayCommand TestConnectionCommand { get; }
    public RelayCommand OpenAiSettingsCommand { get; }
    public RelayCommand CloseAiSettingsCommand { get; }
    public RelayCommand SaveAiSettingsCommand { get; }
    public RelayCommand<string> CopyMessageCommand { get; }

    public AiChatViewModel(
        IAiService aiService,
        AiAnalyticsEngine analyticsEngine,
        IInventoryService? inventoryService = null)
    {
        _aiService = aiService;
        _analyticsEngine = analyticsEngine;
        _inventoryService = inventoryService;

        SendQueryCommand = new AsyncRelayCommand(SendQueryAsync);
        RunBcgCommand = new AsyncRelayCommand(RunBcgAsync);
        RunPeakHoursCommand = new AsyncRelayCommand(RunPeakHoursAsync);
        RunReorderCommand = new AsyncRelayCommand(RunReorderAsync);
        RunRevenueTrendCommand = new AsyncRelayCommand(RunRevenueTrendAsync);
        ApplyAiPoDraftCommand = new AsyncRelayCommand(ApplyAiPoDraftAsync);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);

        OpenAiSettingsCommand = new RelayCommand(() => IsSettingsOpen = true);
        CloseAiSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);
        SaveAiSettingsCommand = new RelayCommand(SaveAiSettings);
        CopyMessageCommand = new RelayCommand<string>(CopyMessage);

        // Welcome message
        Messages.Add(new ChatMessageViewModel
        {
            Sender = "Assistant",
            Content = "Xin chào! Tôi là Trợ lý AI Cục bộ của SmartRestaurant (Mistral-7B / Gemini Cloud Backup). " +
                      "Tôi được cung cấp dữ liệu thực tế từ thực đơn và kho nội bộ qua LINQ Context Grounding để hỗ trợ bạn tư vấn món và phân tích kinh doanh mà không sợ ảo giác số liệu.",
            Timestamp = DateTime.Now
        });

        _ = TestConnectionSilentlyAsync();
    }

    private void CopyMessage(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        try
        {
            Clipboard.SetText(content);
            StatusInfo = "✅ Đã sao chép nội dung vào khay nhớ tạm (Clipboard)!";
        }
        catch (Exception ex)
        {
            StatusInfo = $"Không thể sao chép: {ex.Message}";
        }
    }

    private void SaveAiSettings()
    {
        _aiService.UpdateGeminiConfig(GeminiApiKeyInput, SelectedGeminiModel);
        SettingsFeedback = "✅ Đã lưu cấu hình Google Gemini thành công!";
        StatusInfo = $"Đã cập nhật Gemini Model: {SelectedGeminiModel}";
        Task.Delay(1500).ContinueWith(_ => IsSettingsOpen = false, TaskScheduler.FromCurrentSynchronizationContext());
        _ = TestConnectionSilentlyAsync();
    }

    private async Task TestConnectionSilentlyAsync()
    {
        try
        {
            var report = await _aiService.TestConnectionAsync();
            IsConnectionHealthy = report.IsConnected;
            ConnectionBadgeText = report.IsConnected
                ? $"🟢 {report.Provider} ({report.LatencyMs}ms) - {report.ModelName}"
                : $"⚠️ {report.Provider}";
        }
        catch
        {
            IsConnectionHealthy = false;
            ConnectionBadgeText = "⚠️ AI Ngoại tuyến";
        }
    }

    public async Task TestConnectionAsync()
    {
        IsBusy = true;
        StatusInfo = "Đang kiểm tra kết nối tới LM Studio (Cổng 1235/1234) & Google Gemini Cloud...";

        try
        {
            var report = await _aiService.TestConnectionAsync();
            IsConnectionHealthy = report.IsConnected;
            ConnectionBadgeText = report.IsConnected
                ? $"🟢 {report.Provider} ({report.LatencyMs}ms) - {report.ModelName}"
                : $"⚠️ {report.Provider}";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🔍 **KẾT QUẢ CHẨN ĐOÁN KẾT NỐI AI:**\n");
            sb.AppendLine($"• **Trạng thái:** {(report.IsConnected ? "✅ Kết Nối Tốt (Sẵn Sàng)" : "⚠️ Không Có Kết Nối Trực Tiếp")}");
            sb.AppendLine($"• **Nguồn xử lý (Provider):** {report.Provider}");
            sb.AppendLine($"• **Địa chỉ kết nối (Endpoint):** `{report.Endpoint}`");
            sb.AppendLine($"• **Mô hình AI:** `{report.ModelName}`");
            sb.AppendLine($"• **Thời gian phản hồi (Latency):** `{report.LatencyMs} ms`\n");
            sb.AppendLine($"💬 **Chi tiết:** {report.Message}");

            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = sb.ToString(),
                Timestamp = DateTime.Now
            });

            StatusInfo = $"Hoàn tất chẩn đoán AI: {report.Provider} ({report.LatencyMs}ms).";
        }
        catch (Exception ex)
        {
            IsConnectionHealthy = false;
            ConnectionBadgeText = "❌ Lỗi kết nối";
            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = $"⚠️ Lỗi kiểm tra kết nối: {ex.Message}",
                Timestamp = DateTime.Now
            });
            StatusInfo = "Lỗi khi kiểm tra kết nối AI.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SendQueryAsync()
    {
        if (string.IsNullOrWhiteSpace(InputQuery)) return;

        string query = InputQuery.Trim();
        InputQuery = string.Empty;

        Messages.Add(new ChatMessageViewModel
        {
            Sender = "User",
            Content = query,
            Timestamp = DateTime.Now
        });

        IsBusy = true;
        StatusInfo = "Đang gửi câu hỏi đến mô hình AI...";

        try
        {
            string answer = await _aiService.GetDishRecommendationAsync(query);
            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = answer,
                Timestamp = DateTime.Now
            });
            StatusInfo = "Đã nhận câu trả lời từ AI.";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = $"⚠️ Lỗi kết nối AI: {ex.Message}\n💡 Gợi ý: Nếu dùng Local AI, hãy đảm bảo LM Studio đang chạy trên cổng 1234 hoặc 1235.",
                Timestamp = DateTime.Now
            });
            StatusInfo = "Lỗi kết nối.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunBcgAsync()
    {
        Messages.Add(new ChatMessageViewModel
        {
            Sender = "User",
            Content = "📊 Yêu cầu: Phân tích Ma Trận Thực Đơn BCG (Menu Engineering với Chi Phí BOM).",
            Timestamp = DateTime.Now
        });

        const string cacheKey = "BCG_MATRIX";
        if (_semanticCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheTtl)
        {
            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = cached.Content + "\n\n*(⚡ Trả lời tức thì từ Semantic Cache)*",
                Timestamp = DateTime.Now
            });
            StatusInfo = "Đã tải phân tích BCG từ bộ nhớ đệm.";
            return;
        }

        IsBusy = true;
        StatusInfo = "Đang tính toán ma trận BCG Contribution Margin và gọi AI phân tích...";

        try
        {
            var result = await _analyticsEngine.ComputeBcgMatrixAsync();
            var stars = result.Items.Where(i => i.Classification.StartsWith("Star")).Select(i => $"• {i.DishName}: {i.PortionsSold} phần (Lãi gộp: {i.ContributionMargin:N0} đ, {i.MarginPercentage:N0}%)").ToList();
            var plowhorses = result.Items.Where(i => i.Classification.StartsWith("Plowhorse")).Select(i => $"• {i.DishName}: {i.PortionsSold} phần (Lãi gộp: {i.ContributionMargin:N0} đ, {i.MarginPercentage:N0}%)").ToList();
            var puzzles = result.Items.Where(i => i.Classification.StartsWith("Puzzle")).Select(i => $"• {i.DishName}: {i.PortionsSold} phần (Lãi gộp: {i.ContributionMargin:N0} đ, {i.MarginPercentage:N0}%)").ToList();
            var dogs = result.Items.Where(i => i.Classification.StartsWith("Dog")).Select(i => $"• {i.DishName}: {i.PortionsSold} phần (Lãi gộp: {i.ContributionMargin:N0} đ, {i.MarginPercentage:N0}%)").ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("📊 **KẾT QUẢ MA TRẬN THỰC ĐƠN BCG (MENU ENGINEERING DỰA TRÊN LÃI GỘP BOM):**\n");
            sb.AppendLine($"⭐ **Stars (Ngôi Sao - Bán chạy & Lãi gộp cao):**\n{(stars.Any() ? string.Join("\n", stars) : "• Không có món")}\n");
            sb.AppendLine($"🐄 **Plowhorses (Bò Sữa - Bán chạy nhưng Chi phí BOM cao):**\n{(plowhorses.Any() ? string.Join("\n", plowhorses) : "• Không có món")}\n");
            sb.AppendLine($"❓ **Puzzles (Dấu Hỏi - Tỷ suất sinh lời cao nhưng Bán chậm):**\n{(puzzles.Any() ? string.Join("\n", puzzles) : "• Không có món")}\n");
            sb.AppendLine($"🐕 **Dogs (Chó Mực - Bán chậm & Lãi gộp thấp):**\n{(dogs.Any() ? string.Join("\n", dogs) : "• Không có món")}\n");
            sb.AppendLine("🤖 **TƯ VẤN CHIẾN LƯỢC TỪ GENAI:**");
            sb.AppendLine(result.AiExecutiveNarrative);

            string content = sb.ToString();
            _semanticCache[cacheKey] = (content, DateTime.UtcNow);

            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = content,
                Timestamp = DateTime.Now
            });
            StatusInfo = "Hoàn tất phân tích BCG.";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel { Sender = "Assistant", Content = $"⚠️ Lỗi: {ex.Message}", Timestamp = DateTime.Now });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunPeakHoursAsync()
    {
        Messages.Add(new ChatMessageViewModel
        {
            Sender = "User",
            Content = "📈 Yêu cầu: Phân tích Khung Giờ Cao Điểm & Lưu Lượng Thực Khách.",
            Timestamp = DateTime.Now
        });

        const string cacheKey = "PEAK_HOURS";
        if (_semanticCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheTtl)
        {
            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = cached.Content + "\n\n*(⚡ Trả lời tức thì từ Semantic Cache)*",
                Timestamp = DateTime.Now
            });
            StatusInfo = "Đã tải phân tích giờ cao điểm từ bộ nhớ đệm.";
            return;
        }

        IsBusy = true;
        StatusInfo = "Đang tổng hợp dữ liệu giờ cao điểm...";

        try
        {
            var result = await _analyticsEngine.ComputePeakHoursAsync();
            var busySlots = result.Slots.Where(s => s.OrderVolume > 0).Select(s => $"• Khung giờ {s.TimeSlot}: {s.OrderVolume} đơn ({s.BusynessLevel})").ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("📈 **PHÂN BỐ LƯU LƯỢNG KHÁCH THEO KHUNG GIỜ:**\n");
            sb.AppendLine($"🔥 **Khung giờ cao điểm:** {result.PrimaryPeakPeriod}");
            if (busySlots.Any())
            {
                sb.AppendLine("⏱️ **Lưu lượng theo giờ:**\n" + string.Join("\n", busySlots) + "\n");
            }
            sb.AppendLine("🤖 **KHUYẾN NGHỊ ĐIỀU PHỐI VẬN HÀNH & NHÂN SỰ GENAI:**");
            sb.AppendLine(result.StaffingRecommendations);

            string content = sb.ToString();
            _semanticCache[cacheKey] = (content, DateTime.UtcNow);

            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = content,
                Timestamp = DateTime.Now
            });
            StatusInfo = "Hoàn tất phân tích giờ cao điểm.";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel { Sender = "Assistant", Content = $"⚠️ Lỗi: {ex.Message}", Timestamp = DateTime.Now });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunReorderAsync()
    {
        Messages.Add(new ChatMessageViewModel
        {
            Sender = "User",
            Content = "📦 Yêu cầu: Dự Báo Nhập Kho Nguyên Liệu 7 Ngày Tới & Điểm Đặt Hàng Lại (ROP).",
            Timestamp = DateTime.Now
        });

        IsBusy = true;
        StatusInfo = "Đang phân tích tồn kho, chuỗi cung ứng và ngưỡng tối thiểu...";

        try
        {
            var result = await _analyticsEngine.ComputeWeeklyReorderForecastAsync();
            var urgentItems = result.ReorderList.Where(i => i.Urgency.Contains("Khẩn Cấp")).Select(i => $"• {i.IngredientName}: Tồn kho {i.CurrentStock} (Tối thiểu: {i.MinStock}) -> Cần nhập: {i.RecommendedReorder}").ToList();
            var noticeItems = result.ReorderList.Where(i => i.Urgency.Contains("Lưu Ý")).Select(i => $"• {i.IngredientName}: Tồn kho {i.CurrentStock} -> Cần nhập: {i.RecommendedReorder}").ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("📦 **TỔNG HỢP NGUYÊN VẬT LIỆU CẦN BỔ SUNG (BOM & ROP SUPPLY CHAIN):**\n");
            if (urgentItems.Any())
            {
                sb.AppendLine("🚨 **Cảnh báo khẩn cấp:**\n" + string.Join("\n", urgentItems) + "\n");
            }
            if (noticeItems.Any())
            {
                sb.AppendLine("⚠️ **Cần theo dõi thêm:**\n" + string.Join("\n", noticeItems) + "\n");
            }
            sb.AppendLine("🤖 **KẾ HOẠCH HẬU CẦN & DỰ BÁO TỪ GENAI:**");
            sb.AppendLine(result.AiLogisticsAdvisory);

            if (result.PurchaseOrderDraft != null && result.PurchaseOrderDraft.Any())
            {
                _lastPoDraft = result.PurchaseOrderDraft;
                IsPoDraftAvailable = true;
                sb.AppendLine($"\n💡 **HÀNH ĐỘNG NHANH**: Nhấn nút **'📦 Nhập Kho Theo Đề Xuất PO ({result.PurchaseOrderDraft.Count} món)'** bên dưới để tự động cập nhật kho ngay lập tức!");
            }
            else
            {
                _lastPoDraft = null;
                IsPoDraftAvailable = false;
            }

            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = sb.ToString(),
                Timestamp = DateTime.Now
            });
            StatusInfo = "Hoàn tất dự báo nhập kho.";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel { Sender = "Assistant", Content = $"⚠️ Lỗi: {ex.Message}", Timestamp = DateTime.Now });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyAiPoDraftAsync()
    {
        if (_lastPoDraft == null || !_lastPoDraft.Any() || _inventoryService == null)
        {
            StatusInfo = "Không có phiếu đề xuất nhập kho khả dụng.";
            return;
        }

        IsBusy = true;
        StatusInfo = "Đang thực hiện tự động nhập kho từ đề xuất PO...";

        try
        {
            int count = await _inventoryService.ApplyPurchaseOrderDraftAsync(_lastPoDraft);
            IsPoDraftAvailable = false;
            _lastPoDraft = null;

            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = $"✅ **ĐÃ NHẬP KHO THÀNH CÔNG:**\n" +
                          $"Hệ thống đã tự động bù đắp tồn kho cho **{count} nguyên vật liệu** theo đề xuất của AI và ghi nhận nhật ký kiểm toán (Audit Trail) đầy đủ.",
                Timestamp = DateTime.Now
            });
            StatusInfo = $"Tự động nhập kho thành công {count} nguyên liệu.";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = $"❌ Lỗi khi tự động nhập kho: {ex.Message}",
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunRevenueTrendAsync()
    {
        Messages.Add(new ChatMessageViewModel
        {
            Sender = "User",
            Content = "💰 Yêu cầu: Báo Cáo Phân Tích Xu Hướng Doanh Thu.",
            Timestamp = DateTime.Now
        });

        const string cacheKey = "REVENUE_TRENDS";
        if (_semanticCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheTtl)
        {
            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = cached.Content + "\n\n*(⚡ Trả lời tức thì từ Semantic Cache)*",
                Timestamp = DateTime.Now
            });
            StatusInfo = "Đã tải phân tích doanh thu từ bộ nhớ đệm.";
            return;
        }

        IsBusy = true;
        StatusInfo = "Đang tổng hợp doanh thu tháng qua...";

        try
        {
            var result = await _analyticsEngine.ComputeRevenueInsightsAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
            string topDishesStr = result.TopDishes.Any()
                ? string.Join("\n", result.TopDishes.Select(d => $"• {d}"))
                : "Chưa có dữ liệu món bán trong kỳ.";

            string content = $"💰 **BÁO CÁO DOANH THU KỲ {result.Period}:**\n" +
                             $"• Tổng doanh thu: {result.TotalRevenue:N0} VND\n" +
                             $"• Tổng đơn hoàn tất: {result.TotalOrders} đơn\n\n" +
                             $"🏆 **Top 5 Món Doanh Thu Cao Nhất:**\n{topDishesStr}\n\n" +
                             $"📊 **Đánh Giá & Khuyến Nghị Vận Hành AI:**\n{result.AiAnalysis}";

            _semanticCache[cacheKey] = (content, DateTime.UtcNow);

            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = content,
                Timestamp = DateTime.Now
            });
            StatusInfo = "Hoàn tất phân tích doanh thu.";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel { Sender = "Assistant", Content = $"⚠️ Lỗi: {ex.Message}", Timestamp = DateTime.Now });
        }
        finally
        {
            IsBusy = false;
        }
    }
}
