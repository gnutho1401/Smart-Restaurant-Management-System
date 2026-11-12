using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// ViewModel managing the interactive dining room floor plan and table state transitions.
/// </summary>
public class FloorPlanViewModel : ViewModelBase
{
    private readonly ITableService _tableService;
    private List<DiningTable> _allTables = new();
    private ObservableCollection<DiningTable> _tables = new();
    private DiningTable? _selectedTable;
    private string _selectedArea = "Tất cả";
    private int _availableCount;
    private int _occupiedCount;
    private int _reservedCount;
    private int _cleaningCount;

    public event Action<DiningTable>? RequestOpenPos;
    public event Action<DiningTable>? RequestOpenBilling;

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

    public FloorPlanViewModel(ITableService tableService)
    {
        _tableService = tableService;

        LoadTablesCommand = new AsyncRelayCommand(LoadTablesAsync);
        OpenTableCommand = new AsyncRelayCommand<DiningTable>(ExecuteOpenTableAsync);
        CleanTableDoneCommand = new AsyncRelayCommand<DiningTable>(ExecuteCleanTableDoneAsync);
        ToggleReserveCommand = new AsyncRelayCommand<DiningTable>(ExecuteToggleReserveAsync);
        PayTableCommand = new AsyncRelayCommand<DiningTable>(ExecutePayTableAsync);
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
        if (SelectedArea == "Tất cả")
        {
            Tables = new ObservableCollection<DiningTable>(_allTables);
        }
        else
        {
            Tables = new ObservableCollection<DiningTable>(_allTables.Where(t => t.Area != null && t.Area.AreaName.Contains(SelectedArea)));
        }
    }

    private async Task ExecuteOpenTableAsync(DiningTable? table)
    {
        if (table == null) return;

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
            await _tableService.TransitionTableStatusAsync(table.TableId, TableStatus.Available);
            table.Status = TableStatus.Available;
        }
        UpdateStatistics();
    }
}
