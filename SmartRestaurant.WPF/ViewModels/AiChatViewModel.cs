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
            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = $"📊 **KẾT QUẢ MA TRẬN BCG MENU:**\n\n{result.AiExecutiveNarrative}",
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
            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = $"📈 **PHÂN TÍCH GIỜ CAO ĐIỂM:**\nKhung giờ nóng nhất: {result.PrimaryPeakPeriod}\n\n{result.StaffingRecommendations}",
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
            Messages.Add(new ChatMessageViewModel
            {
                Sender = "Assistant",
                Content = $"📦 **DỰ BÁO NHẬP KHO NGUYÊN LIỆU:**\n\n{result.AiLogisticsAdvisory}",
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
