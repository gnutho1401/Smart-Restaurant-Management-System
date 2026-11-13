using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.WPF.Common;
using SmartRestaurant.WPF.Views;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// ViewModel managing the interactive dining room floor plan and table state transitions.
/// </summary>
public class FloorPlanViewModel : ViewModelBase
{
    private readonly ITableService _tableService;
    private readonly IAuthService? _authService;
    private List<DiningTable> _allTables = new();
    private ObservableCollection<DiningTable> _tables = new();
    private DiningTable? _selectedTable;
    private string _selectedArea = "Tất cả";
    private int _availableCount;
    private int _occupiedCount;
    private int _reservedCount;
    private int _cleaningCount;
    private bool _isToastVisible;
    private string _toastMessage = string.Empty;
    private Func<Task>? _undoAction;
    private System.Threading.CancellationTokenSource? _toastCts;

    public event Action<DiningTable>? RequestOpenPos;
    public event Action<DiningTable>? RequestOpenBilling;

    public bool IsToastVisible
    {
        get => _isToastVisible;
        set => SetProperty(ref _isToastVisible, value);
    }

    public string ToastMessage
    {
        get => _toastMessage;
        set => SetProperty(ref _toastMessage, value);
    }

    public ObservableCollection<DiningTable> Tables
    {
        get => _tables;
        set => SetProperty(ref _tables, value);
    }

    public DiningTable? SelectedTable
    {
        get => _selectedTable;
        set => SetProperty(ref _selectedTable, value);
    }

    public string SelectedArea
    {
        get => _selectedArea;
        set
        {
            if (SetProperty(ref _selectedArea, value))
            {
                ApplyFilter();
            }
        }
    }

    public int AvailableCount
    {
        get => _availableCount;
        set => SetProperty(ref _availableCount, value);
    }

    public int OccupiedCount
    {
        get => _occupiedCount;
        set => SetProperty(ref _occupiedCount, value);
    }

    public int ReservedCount
    {
        get => _reservedCount;
        set => SetProperty(ref _reservedCount, value);
    }

    public int CleaningCount
    {
        get => _cleaningCount;
        set => SetProperty(ref _cleaningCount, value);
    }

    public IAsyncRelayCommand LoadTablesCommand { get; }
    public IAsyncRelayCommand<DiningTable> OpenTableCommand { get; }
    public IAsyncRelayCommand<DiningTable> CleanTableDoneCommand { get; }
    public IAsyncRelayCommand<DiningTable> ToggleReserveCommand { get; }
    public IAsyncRelayCommand<DiningTable> PayTableCommand { get; }
    public IAsyncRelayCommand<DiningTable> TransferTableCommand { get; }
    public IAsyncRelayCommand<DiningTable> MergeTableCommand { get; }
    public IAsyncRelayCommand UndoToastCommand { get; }

    public FloorPlanViewModel(ITableService tableService, IAuthService? authService = null)
    {
        _tableService = tableService;
        _authService = authService;

        LoadTablesCommand = new AsyncRelayCommand(LoadTablesAsync);
        OpenTableCommand = new AsyncRelayCommand<DiningTable>(ExecuteOpenTableAsync);
        CleanTableDoneCommand = new AsyncRelayCommand<DiningTable>(ExecuteCleanTableDoneAsync);
        ToggleReserveCommand = new AsyncRelayCommand<DiningTable>(ExecuteToggleReserveAsync);
        PayTableCommand = new AsyncRelayCommand<DiningTable>(ExecutePayTableAsync);
        TransferTableCommand = new AsyncRelayCommand<DiningTable>(ExecuteTransferTableAsync);
        MergeTableCommand = new AsyncRelayCommand<DiningTable>(ExecuteMergeTableAsync);
        UndoToastCommand = new AsyncRelayCommand(ExecuteUndoToastAsync);
    }

    public async Task LoadTablesAsync()
    {
        var result = await _tableService.GetAllTablesAsync();
        _allTables = result.ToList();
        UpdateStatistics();
        ApplyFilter();
    }

    private void UpdateStatistics()
    {
        AvailableCount = _allTables.Count(t => t.Status == TableStatus.Available);
        OccupiedCount = _allTables.Count(t => t.Status == TableStatus.Occupied);
        ReservedCount = _allTables.Count(t => t.Status == TableStatus.Reserved);
        CleaningCount = _allTables.Count(t => t.Status == TableStatus.Cleaning);
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SelectedArea) || SelectedArea == "Tất cả")
        {
            Tables = new ObservableCollection<DiningTable>(_allTables);
        }
        else
        {
            Tables = new ObservableCollection<DiningTable>(_allTables.Where(t => t.Area != null && t.Area.AreaName.Contains(SelectedArea, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private async Task ExecuteOpenTableAsync(DiningTable? table)
    {
        if (table == null) return;

        // Workflow Guard: Cannot open table for ordering if table is currently waiting for cleaning
        if (table.Status == TableStatus.Cleaning)
        {
            System.Windows.MessageBox.Show(
                $"Bàn {table.TableName} đang trong trạng thái chờ dọn dẹp. Vui lòng bấm 'Dọn Xong' trước khi mở bàn đón khách mới!",
                "Chốt Chặn Quy Trình Vận Hành",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (table.Status == TableStatus.Available || table.Status == TableStatus.Reserved)
        {
            await _tableService.TransitionTableStatusAsync(table.TableId, TableStatus.Occupied);
            table.Status = TableStatus.Occupied;
            UpdateStatistics();
        }

        RequestOpenPos?.Invoke(table);
    }

    private Task ExecutePayTableAsync(DiningTable? table)
    {
        if (table == null) return Task.CompletedTask;

        // Workflow Guard: Can only settle billing for tables currently occupied
        if (table.Status != TableStatus.Occupied)
        {
            System.Windows.MessageBox.Show(
                $"Bàn {table.TableName} hiện chưa có khách hoặc chưa gọi món để quyết toán.",
                "Chốt Chặn Quyết Toán Hóa Đơn",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return Task.CompletedTask;
        }

        RequestOpenBilling?.Invoke(table);
        return Task.CompletedTask;
    }

    private async Task ExecuteCleanTableDoneAsync(DiningTable? table)
    {
        if (table == null || table.Status != TableStatus.Cleaning) return;

        // DFA: Cleaning -> Available
        await _tableService.TransitionTableStatusAsync(table.TableId, TableStatus.Available);
        table.Status = TableStatus.Available;
        UpdateStatistics();

        // 5-second Undo Banner
        ShowToastWithUndo($"Bàn {table.TableName} đã dọn xong (Sẵn Sàng).", async () =>
        {
            await _tableService.RevertTableStatusAsync(table.TableId, TableStatus.Cleaning);
            table.Status = TableStatus.Cleaning;
            UpdateStatistics();
        });
    }

    private async Task ExecuteToggleReserveAsync(DiningTable? table)
    {
        if (table == null) return;

        if (table.Status == TableStatus.Available)
        {
            await _tableService.TransitionTableStatusAsync(table.TableId, TableStatus.Reserved);
            table.Status = TableStatus.Reserved;
        }
        else if (table.Status == TableStatus.Reserved)
        {
            // Subordinate protection: Cashier cannot cancel reservations made by management
            if (_authService?.CurrentUser?.Role == UserRole.Cashier)
            {
                System.Windows.MessageBox.Show(
                    $"Bàn {table.TableName} đã được đặt trước bởi Quản lý hoặc Khách VIP. Thu ngân không có quyền tự ý hủy bàn đặt trước của cấp trên. Vui lòng liên hệ Quản Lý (Store Manager)!",
                    "Chặn Can Thiệp Cấp Trên",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            await _tableService.TransitionTableStatusAsync(table.TableId, TableStatus.Available);
            table.Status = TableStatus.Available;

            // 5-second Undo Banner
            ShowToastWithUndo($"Đã hủy đặt trước bàn {table.TableName}.", async () =>
            {
                await _tableService.RevertTableStatusAsync(table.TableId, TableStatus.Reserved);
                table.Status = TableStatus.Reserved;
                UpdateStatistics();
            });
        }
        UpdateStatistics();
    }

    public void ShowToastWithUndo(string message, Func<Task> undoAction)
    {
        _toastCts?.Cancel();
        _toastCts = new System.Threading.CancellationTokenSource();
        var token = _toastCts.Token;

        ToastMessage = message;
        _undoAction = undoAction;
        IsToastVisible = true;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(5000, token);
                if (!token.IsCancellationRequested)
                {
                    if (System.Windows.Application.Current != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            IsToastVisible = false;
                            _undoAction = null;
                        });
                    }
                    else
                    {
                        IsToastVisible = false;
                        _undoAction = null;
                    }
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private async Task ExecuteUndoToastAsync()
    {
        _toastCts?.Cancel();
        IsToastVisible = false;

        if (_undoAction != null)
        {
            var action = _undoAction;
            _undoAction = null;
            await action.Invoke();
        }
    }

    private async Task ExecuteTransferTableAsync(DiningTable? table)
    {
        if (table == null) return;

        if (table.Status != TableStatus.Occupied)
        {
            System.Windows.MessageBox.Show(
                $"Chỉ có thể chuyển bàn đang có khách dùng bữa (Occupied). Bàn {table.TableName} hiện đang ở trạng thái {table.Status}.",
                "Nghiệp Vụ Chuyển Bàn",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var dialog = new TableTransferMergeDialog(table, _allTables, _tableService, defaultToMerge: false);
        if (dialog.ShowDialog() == true)
        {
            await LoadTablesAsync();
        }
    }

    private async Task ExecuteMergeTableAsync(DiningTable? table)
    {
        if (table == null) return;

        if (table.Status != TableStatus.Occupied)
        {
            System.Windows.MessageBox.Show(
                $"Chỉ có thể gộp bàn đang có khách dùng bữa (Occupied). Bàn {table.TableName} hiện đang ở trạng thái {table.Status}.",
                "Nghiệp Vụ Gộp Bàn",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var dialog = new TableTransferMergeDialog(table, _allTables, _tableService, defaultToMerge: true);
        if (dialog.ShowDialog() == true)
        {
            await LoadTablesAsync();
        }
    }
}
