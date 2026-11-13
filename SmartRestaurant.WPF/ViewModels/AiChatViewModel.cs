using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SmartRestaurant.AIEngine;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// ViewModel driving the conversational Local GenAI Assistant and business intelligence insights.
/// </summary>
public class AiChatViewModel : ViewModelBase
{
    private readonly IAiService _aiService;
    private readonly AiAnalyticsEngine _analyticsEngine;

    private ObservableCollection<ChatMessageViewModel> _messages = new();
    private string _inputQuery = string.Empty;
    private bool _isBusy;
    private string _statusInfo = "Sẵn sàng kết nối LM Studio (localhost:1234 / 1235)";

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

    public IAsyncRelayCommand SendQueryCommand { get; }
    public IAsyncRelayCommand RunBcgCommand { get; }
    public IAsyncRelayCommand RunPeakHoursCommand { get; }
    public IAsyncRelayCommand RunReorderCommand { get; }
    public IAsyncRelayCommand RunRevenueTrendCommand { get; }

    public AiChatViewModel(IAiService aiService, AiAnalyticsEngine analyticsEngine)
    {
        _aiService = aiService;
        _analyticsEngine = analyticsEngine;

        SendQueryCommand = new AsyncRelayCommand(SendQueryAsync);
        RunBcgCommand = new AsyncRelayCommand(RunBcgAsync);
        RunPeakHoursCommand = new AsyncRelayCommand(RunPeakHoursAsync);
        RunReorderCommand = new AsyncRelayCommand(RunReorderAsync);
        RunRevenueTrendCommand = new AsyncRelayCommand(RunRevenueTrendAsync);

        // Welcome message
        Messages.Add(new ChatMessageViewModel
        {
            Sender = "Assistant",
            Content = "Xin chào! Tôi là Trợ lý AI Cục bộ của SmartRestaurant (Mistral-7B). " +
                      "Tôi được cung cấp dữ liệu thực tế từ thực đơn và kho nội bộ qua LINQ Context Grounding để hỗ trợ bạn tư vấn món và phân tích kinh doanh mà không sợ ảo giác số liệu.",
            Timestamp = DateTime.Now
        });
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
        StatusInfo = "Đang gửi câu hỏi đến LM Studio cục bộ...";

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
                Content = $"⚠️ Lỗi kết nối AI: {ex.Message}",
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
            Content = "📊 Yêu cầu: Phân tích Ma Trận Thực Đơn BCG (Menu Engineering).",
            Timestamp = DateTime.Now
        });

        IsBusy = true;
        StatusInfo = "Đang tính toán ma trận BCG và gọi AI phân tích...";

        try
        {
            var result = await _analyticsEngine.ComputeBcgMatrixAsync();
            var stars = result.Items.Where(i => i.Classification.StartsWith("Star")).Select(i => $"• {i.DishName}: {i.PortionsSold} phần ({i.Revenue:N0} đ)").ToList();
            var plowhorses = result.Items.Where(i => i.Classification.StartsWith("Plowhorse")).Select(i => $"• {i.DishName}: {i.PortionsSold} phần ({i.Revenue:N0} đ)").ToList();
            var puzzles = result.Items.Where(i => i.Classification.StartsWith("Puzzle")).Select(i => $"• {i.DishName}: {i.PortionsSold} phần ({i.Revenue:N0} đ)").ToList();
            var dogs = result.Items.Where(i => i.Classification.StartsWith("Dog")).Select(i => $"• {i.DishName}: {i.PortionsSold} phần ({i.Revenue:N0} đ)").ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("📊 **KẾT QUẢ MA TRẬN THỰC ĐƠN BCG (MENU ENGINEERING):**\n");
            sb.AppendLine($"⭐ **Stars (Ngôi Sao - Bán chạy & Sinh lời cao):**\n{(stars.Any() ? string.Join("\n", stars) : "• Không có món")}\n");
            sb.AppendLine($"🐄 **Plowhorses (Bò Sữa - Bán chạy, Giá phổ thông):**\n{(plowhorses.Any() ? string.Join("\n", plowhorses) : "• Không có món")}\n");
            sb.AppendLine($"❓ **Puzzles (Dấu Hỏi - Giá trị cao, Bán chậm):**\n{(puzzles.Any() ? string.Join("\n", puzzles) : "• Không có món")}\n");
            sb.AppendLine($"🐕 **Dogs (Chó Mực - Bán chậm, Lợi nhuận thấp):**\n{(dogs.Any() ? string.Join("\n", dogs) : "• Không có món")}\n");
            sb.AppendLine("🤖 **TƯ VẤN CHIẾN LƯỢC TỪ GENAI MISTRAL-7B:**");
            sb.AppendLine(result.AiExecutiveNarrative);

            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = sb.ToString(),
                Timestamp = DateTime.Now
            });
            StatusInfo = "Hoàn tất phân tích BCG.";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel { Sender = "Assistant", Content = $"⚠️ Lỗi: {ex.Message}" });
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

            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = sb.ToString(),
                Timestamp = DateTime.Now
            });
            StatusInfo = "Hoàn tất phân tích giờ cao điểm.";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel { Sender = "Assistant", Content = $"⚠️ Lỗi: {ex.Message}" });
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
            Content = "📦 Yêu cầu: Dự Báo Nhập Kho Nguyên Liệu 7 Ngày Tới.",
            Timestamp = DateTime.Now
        });

        IsBusy = true;
        StatusInfo = "Đang phân tích tồn kho và ngưỡng tối thiểu...";

        try
        {
            var result = await _analyticsEngine.ComputeWeeklyReorderForecastAsync();
            var urgentItems = result.ReorderList.Where(i => i.Urgency.Contains("Khẩn Cấp")).Select(i => $"• {i.IngredientName}: Tồn kho {i.CurrentStock} (Tối thiểu: {i.MinStock}) -> Cần nhập: {i.RecommendedReorder}").ToList();
            var noticeItems = result.ReorderList.Where(i => i.Urgency.Contains("Lưu Ý")).Select(i => $"• {i.IngredientName}: Tồn kho {i.CurrentStock} -> Cần nhập: {i.RecommendedReorder}").ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("📦 **TỔNG HỢP NGUYÊN VẬT LIỆU CẦN BỔ SUNG (BOM):**\n");
            if (urgentItems.Any())
            {
                sb.AppendLine("🚨 **Cảnh báo khẩn cấp:**\n" + string.Join("\n", urgentItems) + "\n");
            }
            if (noticeItems.Any())
            {
                sb.AppendLine("⚠️ **Cần theo dõi thêm:**\n" + string.Join("\n", noticeItems) + "\n");
            }
            sb.AppendLine("🤖 **KẾ HOẠCH HẬU CẦN & DỰ BÁO TỪ GENAI MISTRAL-7B:**");
            sb.AppendLine(result.AiLogisticsAdvisory);

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
            Messages.Add(new ChatMessageViewModel { Sender = "Assistant", Content = $"⚠️ Lỗi: {ex.Message}" });
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

        IsBusy = true;
        StatusInfo = "Đang tổng hợp doanh thu tháng qua...";

        try
        {
            var result = await _analyticsEngine.ComputeRevenueInsightsAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
            string topDishesStr = result.TopDishes.Any()
                ? string.Join("\n", result.TopDishes.Select(d => $"• {d}"))
                : "Chưa có dữ liệu món bán trong kỳ.";

            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = $"💰 **BÁO CÁO DOANH THU KỲ {result.Period}:**\n" +
                          $"• Tổng doanh thu: {result.TotalRevenue:N0} VND\n" +
                          $"• Tổng đơn hoàn tất: {result.TotalOrders} đơn\n\n" +
                          $"🏆 **Top 5 Món Doanh Thu Cao Nhất:**\n{topDishesStr}\n\n" +
                          $"📊 **Đánh Giá & Khuyến Nghị Vận Hành AI:**\n{result.AiAnalysis}",
                Timestamp = DateTime.Now
            });
            StatusInfo = "Hoàn tất phân tích doanh thu.";
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel { Sender = "Assistant", Content = $"⚠️ Lỗi: {ex.Message}" });
        }
        finally
        {
            IsBusy = false;
        }
    }
}
