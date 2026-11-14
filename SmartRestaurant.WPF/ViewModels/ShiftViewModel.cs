using System;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// ViewModel managing cashier shift lifecycle, cash drawer float, and end-of-day Z-Report reconciliation.
/// </summary>
public class ShiftViewModel : ViewModelBase
{
    private readonly IShiftService _shiftService;
    private readonly IAuthService? _authService;

    private WorkShift? _currentShift;
    private decimal _openingFloatInput = 1000000m; // Default standard 1,000,000 VND
    private decimal _actualCashEndInput;
    private decimal _cashVariance;
    private string _statusMessage = string.Empty;
    private bool _hasActiveShift;
    private string _varianceDescription = "Chưa kết ca kiểm đếm";

    public WorkShift? CurrentShift
    {
        get => _currentShift;
        set
        {
            if (SetProperty(ref _currentShift, value))
            {
                HasActiveShift = value != null && !value.IsClosed;
                OnPropertyChanged(nameof(ShiftStartTimeText));
                OnPropertyChanged(nameof(OpeningFloatText));
                OnPropertyChanged(nameof(CashSalesText));
                OnPropertyChanged(nameof(CardSalesText));
                OnPropertyChanged(nameof(QrSalesText));
                OnPropertyChanged(nameof(TotalSalesText));
                OnPropertyChanged(nameof(ExpectedCashEndText));
            }
        }
    }

    public bool HasActiveShift
    {
        get => _hasActiveShift;
        set => SetProperty(ref _hasActiveShift, value);
    }

    public decimal OpeningFloatInput
    {
        get => _openingFloatInput;
        set => SetProperty(ref _openingFloatInput, value);
    }

    public decimal ActualCashEndInput
    {
        get => _actualCashEndInput;
        set
        {
            if (SetProperty(ref _actualCashEndInput, value))
            {
                RecalculateVariance();
            }
        }
    }

    public decimal CashVariance
    {
        get => _cashVariance;
        set => SetProperty(ref _cashVariance, value);
    }

    public string VarianceDescription
    {
        get => _varianceDescription;
        set => SetProperty(ref _varianceDescription, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string CashierName => _authService?.CurrentUser?.FullName ?? "Thu Ngân";
    public string ShiftStartTimeText => CurrentShift?.StartTime.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "--:--";
    public string OpeningFloatText => (CurrentShift?.OpeningFloat ?? OpeningFloatInput).ToString("N0") + " đ";
    public string CashSalesText => (CurrentShift?.CashSales ?? 0).ToString("N0") + " đ";
    public string CardSalesText => (CurrentShift?.CardSales ?? 0).ToString("N0") + " đ";
    public string QrSalesText => (CurrentShift?.QrSales ?? 0).ToString("N0") + " đ";
    public string TotalSalesText => (CurrentShift?.TotalSales ?? 0).ToString("N0") + " đ";
    public string ExpectedCashEndText => ((CurrentShift?.OpeningFloat ?? 0) + (CurrentShift?.CashSales ?? 0)).ToString("N0") + " đ";

    public IAsyncRelayCommand LoadShiftCommand { get; }
    public IAsyncRelayCommand StartShiftCommand { get; }
    public IAsyncRelayCommand CloseShiftCommand { get; }

    public ShiftViewModel(IShiftService shiftService, IAuthService? authService = null)
    {
        _shiftService = shiftService;
        _authService = authService;

        LoadShiftCommand = new AsyncRelayCommand(LoadShiftAsync);
        StartShiftCommand = new AsyncRelayCommand(StartShiftAsync);
        CloseShiftCommand = new AsyncRelayCommand(CloseShiftAsync);
    }

    public async Task LoadShiftAsync()
    {
        int userId = _authService?.CurrentUser?.UserId ?? 1;
        var shift = await _shiftService.GetCurrentOpenShiftAsync(userId);
        CurrentShift = shift;

        if (shift != null)
        {
            ActualCashEndInput = shift.OpeningFloat + shift.CashSales;
            RecalculateVariance();
            StatusMessage = $"Ca làm việc đang mở từ {shift.StartTime.ToLocalTime():HH:mm}.";
        }
        else
        {
            StatusMessage = "Hiện chưa có ca làm việc nào đang mở. Vui lòng nhập tiền đầu ca để bắt đầu.";
        }
    }

    private async Task StartShiftAsync()
    {
        if (OpeningFloatInput < 0)
        {
            StatusMessage = "Tiền mặt đầu ca không thể là số âm.";
            return;
        }

        try
        {
            int userId = _authService?.CurrentUser?.UserId ?? 1;
            var shift = await _shiftService.OpenShiftAsync(userId, OpeningFloatInput);
            CurrentShift = shift;
            ActualCashEndInput = shift.OpeningFloat;
            RecalculateVariance();
            StatusMessage = $"Đã mở ca làm việc thành công lúc {shift.StartTime.ToLocalTime():HH:mm}!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể mở ca: {ex.Message}";
        }
    }

    private void RecalculateVariance()
    {
        if (CurrentShift == null) return;
        decimal expectedCash = CurrentShift.OpeningFloat + CurrentShift.CashSales;
        CashVariance = ActualCashEndInput - expectedCash;

        if (CashVariance == 0)
        {
            VarianceDescription = "Khớp tiền 100% (Không chênh lệch)";
        }
        else if (CashVariance > 0)
        {
            VarianceDescription = $"Thừa tiền: +{CashVariance:N0} đ";
        }
        else
        {
            VarianceDescription = $"Thâm hụt tiền: {CashVariance:N0} đ";
        }
    }

    private async Task CloseShiftAsync()
    {
        if (CurrentShift == null) return;

        try
        {
            var closedShift = await _shiftService.CloseShiftAsync(
                CurrentShift.ShiftId, 
                ActualCashEndInput, 
                $"Chốt ca bởi {CashierName}. Chênh lệch: {CashVariance:N0} đ");

            CurrentShift = closedShift;
            HasActiveShift = false;
            StatusMessage = $"Đã chốt ca thành công! Báo cáo Z-Report đã được ghi nhận. {VarianceDescription}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể chốt ca: {ex.Message}";
        }
    }
}