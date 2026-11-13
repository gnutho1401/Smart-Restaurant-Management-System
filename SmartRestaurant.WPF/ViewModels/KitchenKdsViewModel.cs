using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// ViewModel orchestrating the Wall-Mounted Kitchen Display System (KDS) live queue
/// using a non-blocking background PeriodicTimer polling engine.
/// </summary>
public class KitchenKdsViewModel : ViewModelBase, IDisposable
{
    private readonly IKdsService _kdsService;
    private readonly IOrderService _orderService;
    private readonly IAuthService? _authService;
    private ObservableCollection<KitchenTicketViewModel> _tickets = new();
    private int _pendingCount;
    private int _cookingCount;
    private int _readyCount;

    private CancellationTokenSource? _timerCts;
    private PeriodicTimer? _periodicTimer;

    public ObservableCollection<KitchenTicketViewModel> Tickets
    {
        get => _tickets;
        set => SetProperty(ref _tickets, value);
    }

    public int PendingCount
    {
        get => _pendingCount;
        set => SetProperty(ref _pendingCount, value);
    }

    public int CookingCount
    {
        get => _cookingCount;
        set => SetProperty(ref _cookingCount, value);
    }

    public int ReadyCount
    {
        get => _readyCount;
        set => SetProperty(ref _readyCount, value);
    }

    public IAsyncRelayCommand LoadTicketsCommand { get; }
    public IAsyncRelayCommand<KitchenTicketViewModel> AdvanceStatusCommand { get; }
    public IAsyncRelayCommand<KitchenTicketViewModel> VoidTicketCommand { get; }

    public KitchenKdsViewModel(
        IKdsService kdsService,
        IOrderService orderService,
        IAuthService? authService = null)
    {
        _kdsService = kdsService;
        _orderService = orderService;
        _authService = authService;

        LoadTicketsCommand = new AsyncRelayCommand(LoadTicketsAsync);
        AdvanceStatusCommand = new AsyncRelayCommand<KitchenTicketViewModel>(AdvanceStatusAsync);
        VoidTicketCommand = new AsyncRelayCommand<KitchenTicketViewModel>(VoidTicketAsync);

        StartPollingEngine();
    }

    public async Task LoadTicketsAsync()
    {
        var rawTickets = await _kdsService.GetActiveKitchenTicketsAsync();

        var viewModels = rawTickets.Select(t => new KitchenTicketViewModel
        {
            OrderItemId = t.OrderItemId,
            OrderId = t.OrderId,
            TableName = t.Order?.Table?.TableName ?? $"Bàn #{t.Order?.TableId}",
            DishName = t.MenuItem?.Name ?? "Món ăn",
            Quantity = t.Quantity,
            AllergyNote = t.Note,
            CreatedAt = t.Order?.CreatedAt ?? DateTime.UtcNow,
            Status = t.CookingStatus
        }).ToList();

        Tickets = new ObservableCollection<KitchenTicketViewModel>(viewModels);

        PendingCount = Tickets.Count(t => t.Status == CookingStatus.Pending);
        CookingCount = Tickets.Count(t => t.Status == CookingStatus.Cooking);
        ReadyCount = Tickets.Count(t => t.Status == CookingStatus.Ready);
    }

    private async Task VoidTicketAsync(KitchenTicketViewModel? ticket)
    {
        if (ticket == null) return;

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn HỦY món '{ticket.DishName}' ({ticket.TableName}) không?\n\nLưu ý: Nếu món đang ở trạng thái 'Chờ Nấu', nguyên liệu BOM sẽ tự động được hoàn trả vào kho.",
            "Xác Nhận Hủy Món Nhà Bếp",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        int userId = _authService?.CurrentUser?.UserId ?? 1;
        bool success = await _orderService.VoidOrderItemAsync(ticket.OrderItemId, "Khách yêu cầu hủy món qua KDS", userId);

        if (success)
        {
            Tickets.Remove(ticket);
            PendingCount = Tickets.Count(t => t.Status == CookingStatus.Pending);
            CookingCount = Tickets.Count(t => t.Status == CookingStatus.Cooking);
            ReadyCount = Tickets.Count(t => t.Status == CookingStatus.Ready);

            MessageBox.Show(
                $"Đã hủy thành công món '{ticket.DishName}' của {ticket.TableName}!",
                "Đã Hủy Món",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Không thể hủy món này hoặc món đã bị hủy trước đó.", "Lỗi Hủy Món", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task AdvanceStatusAsync(KitchenTicketViewModel? ticket)
    {
        if (ticket == null) return;

        var nextStatus = ticket.Status switch
        {
            CookingStatus.Pending => CookingStatus.Cooking,
            CookingStatus.Cooking => CookingStatus.Ready,
            CookingStatus.Ready => CookingStatus.Served,
            _ => CookingStatus.Served
        };

        await _kdsService.UpdateTicketStatusAsync(ticket.OrderItemId, nextStatus);

        if (nextStatus == CookingStatus.Served)
        {
            Tickets.Remove(ticket);
        }
        else
        {
            ticket.Status = nextStatus;
        }

        PendingCount = Tickets.Count(t => t.Status == CookingStatus.Pending);
        CookingCount = Tickets.Count(t => t.Status == CookingStatus.Cooking);
        ReadyCount = Tickets.Count(t => t.Status == CookingStatus.Ready);
    }

    private void StartPollingEngine()
    {
        _timerCts = new CancellationTokenSource();
        _periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(3.0));

        Task.Run(async () =>
        {
            try
            {
                while (await _periodicTimer.WaitForNextTickAsync(_timerCts.Token))
                {
                    if (Application.Current != null)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            await LoadTicketsAsync();
                            foreach (var ticket in Tickets)
                            {
                                ticket.RefreshTimer();
                            }
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful timer cancellation
            }
        });
    }

    public void Dispose()
    {
        _timerCts?.Cancel();
        _periodicTimer?.Dispose();
        _timerCts?.Dispose();
    }
}
