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
    private bool _isChimeEnabled = true;
    private HashSet<int> _knownPendingTicketIds = new();

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

    public bool IsChimeEnabled
    {
        get => _isChimeEnabled;
        set
        {
            if (SetProperty(ref _isChimeEnabled, value))
            {
                OnPropertyChanged(nameof(ChimeButtonText));
            }
        }
    }

    public string ChimeButtonText => IsChimeEnabled ? "Chuông: BẬT" : "Chuông: TẮT";

    public IAsyncRelayCommand LoadTicketsCommand { get; }
    public IAsyncRelayCommand<KitchenTicketViewModel> AdvanceStatusCommand { get; }
    public IAsyncRelayCommand<KitchenTicketViewModel> VoidTicketCommand { get; }
    public RelayCommand ToggleChimeCommand { get; }

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
        ToggleChimeCommand = new RelayCommand(() => IsChimeEnabled = !IsChimeEnabled);

        StartPollingEngine();
    }

    public async Task LoadTicketsAsync()
    {
        var rawTickets = await _kdsService.GetActiveKitchenTicketsAsync();

        var currentPendingIds = rawTickets
            .Where(t => t.CookingStatus == CookingStatus.Pending)
            .Select(t => t.OrderItemId)
            .ToHashSet();

        // Trigger KDS audio chime if new order items arrived from POS
        if (_knownPendingTicketIds.Count > 0 && currentPendingIds.Any(id => !_knownPendingTicketIds.Contains(id)))
        {
            if (IsChimeEnabled)
            {
                PlayKitchenChime();
            }
        }
        _knownPendingTicketIds = currentPendingIds;

        // Differential in-place synchronization: Eliminates UI flicker and avoids rebuilding visual cards
        var incomingDict = rawTickets.ToDictionary(t => t.OrderItemId);

        // 1. Remove tickets that have been served or voided
        for (int i = Tickets.Count - 1; i >= 0; i--)
        {
            if (!incomingDict.ContainsKey(Tickets[i].OrderItemId))
            {
                Tickets.RemoveAt(i);
            }
        }

        // 2. Update existing tickets in-place or append newly arrived tickets
        foreach (var raw in rawTickets)
        {
            var existing = Tickets.FirstOrDefault(t => t.OrderItemId == raw.OrderItemId);
            if (existing != null)
            {
                if (existing.Status != raw.CookingStatus)
                {
                    existing.Status = raw.CookingStatus;
                }
            }
            else
            {
                Tickets.Add(new KitchenTicketViewModel
                {
                    OrderItemId = raw.OrderItemId,
                    OrderId = raw.OrderId,
                    TableName = raw.Order?.Table?.TableName ?? $"Bàn #{raw.Order?.TableId}",
                    DishName = raw.MenuItem?.Name ?? "Món ăn",
                    Quantity = raw.Quantity,
                    AllergyNote = raw.Note,
                    CreatedAt = raw.Order?.CreatedAt ?? DateTime.UtcNow,
                    Status = raw.CookingStatus
                });
            }
        }

        PendingCount = Tickets.Count(t => t.Status == CookingStatus.Pending);
        CookingCount = Tickets.Count(t => t.Status == CookingStatus.Cooking);
        ReadyCount = Tickets.Count(t => t.Status == CookingStatus.Ready);
    }

    public void PlayKitchenChime()
    {
        Task.Run(() =>
        {
            try
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
            catch
            {
                try { Console.Beep(1200, 150); Console.Beep(1600, 250); } catch { }
            }
        });
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

    private volatile bool _isPollingActive = true;

    public void PausePolling() => _isPollingActive = false;

    public void ResumePolling()
    {
        _isPollingActive = true;
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
                    if (!_isPollingActive) continue;

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
