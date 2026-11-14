using System;
using System.Windows.Media;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// Presentation model for kitchen ticket cards with color-coded urgency elapsed timers.
/// Green: &lt; 10 mins | Yellow: 10–20 mins | Red: &gt; 20 mins overdue.
/// </summary>
public class KitchenTicketViewModel : ViewModelBase
{
    private static readonly SolidColorBrush GreenUrgency = new((Color)ColorConverter.ConvertFromString("#2ECC71"));
    private static readonly SolidColorBrush YellowUrgency = new((Color)ColorConverter.ConvertFromString("#F1C40F"));
    private static readonly SolidColorBrush RedUrgency = new((Color)ColorConverter.ConvertFromString("#E74C3C"));

    private CookingStatus _status;

    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string DishName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? AllergyNote { get; set; }
    public DateTime CreatedAt { get; set; }

    public CookingStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public int ElapsedMinutes => (int)(DateTime.UtcNow - CreatedAt).TotalMinutes;

    public string ElapsedDisplay => $"{Math.Max(1, ElapsedMinutes)} phút trước";

    public SolidColorBrush UrgencyBrush => ElapsedMinutes switch
    {
        < 10 => GreenUrgency,
        <= 20 => YellowUrgency,
        _ => RedUrgency
    };

    public string ActionButtonText => Status switch
    {
        CookingStatus.Pending => "Bắt Đầu Nấu",
        CookingStatus.Cooking => "Nấu Xong (Sẵn Sàng)",
        CookingStatus.Ready => "Đã Phục Vụ",
        _ => "Hoàn tất"
    };

    public void RefreshTimer()
    {
        OnPropertyChanged(nameof(ElapsedMinutes));
        OnPropertyChanged(nameof(ElapsedDisplay));
        OnPropertyChanged(nameof(UrgencyBrush));
    }
}
