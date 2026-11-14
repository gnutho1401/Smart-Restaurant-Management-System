using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.AIEngine;
using SmartRestaurant.AIEngine.Models;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// ViewModel driving the Executive Business Intelligence & AI Analytics Dashboard.
/// </summary>
public class ReportsViewModel : ViewModelBase
{
    private readonly RestaurantDbContext _context;
    private readonly AiAnalyticsEngine _analyticsEngine;
    private readonly IInventoryService _inventoryService;
    private readonly ITableService _tableService;

    private decimal _totalRevenue;
    private int _completedOrdersCount;
    private decimal _averageOrderValue;
    private int _activeTablesCount;
    private int _lowStockCount;
    private string _peakHoursSummary = "Đang tải dữ liệu...";
    private string _inventoryForecastSummary = "Đang phân tích...";
    private bool _isLoading;

    private ObservableCollection<BcgItem> _bcgItems = new();

    public decimal TotalRevenue
    {
        get => _totalRevenue;
        set => SetProperty(ref _totalRevenue, value);
    }

    public int CompletedOrdersCount
    {
        get => _completedOrdersCount;
        set => SetProperty(ref _completedOrdersCount, value);
    }

    public decimal AverageOrderValue
    {
        get => _averageOrderValue;
        set => SetProperty(ref _averageOrderValue, value);
    }

    public int ActiveTablesCount
    {
        get => _activeTablesCount;
        set => SetProperty(ref _activeTablesCount, value);
    }

    public int LowStockCount
    {
        get => _lowStockCount;
        set => SetProperty(ref _lowStockCount, value);
    }

    public string PeakHoursSummary
    {
        get => _peakHoursSummary;
        set => SetProperty(ref _peakHoursSummary, value);
    }

    public string InventoryForecastSummary
    {
        get => _inventoryForecastSummary;
        set => SetProperty(ref _inventoryForecastSummary, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ObservableCollection<BcgItem> BcgItems
    {
        get => _bcgItems;
        set => SetProperty(ref _bcgItems, value);
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public ReportsViewModel(
        RestaurantDbContext context,
        AiAnalyticsEngine analyticsEngine,
        IInventoryService inventoryService,
        ITableService tableService)
    {
        _context = context;
        _analyticsEngine = analyticsEngine;
        _inventoryService = inventoryService;
        _tableService = tableService;

        RefreshCommand = new AsyncRelayCommand(LoadDashboardAsync);
    }

    public async Task LoadDashboardAsync()
    {
        IsLoading = true;
        try
        {
            // 1. Calculate Revenue & Orders via SQL Server Aggregation (.AsNoTracking)
            var invoiceStats = await _context.Invoices
                .AsNoTracking()
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Sum(i => i.TotalDue),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            TotalRevenue = invoiceStats?.Total ?? 0m;
            CompletedOrdersCount = invoiceStats?.Count ?? 0;
            AverageOrderValue = CompletedOrdersCount > 0 ? TotalRevenue / CompletedOrdersCount : 0m;

            // 2. Active Tables & Low Stock Alerts
            var tables = await _tableService.GetAllTablesAsync();
            ActiveTablesCount = tables.Count(t => t.Status == TableStatus.Occupied);

            var lowStock = await _inventoryService.GetLowStockAlertsAsync();
            LowStockCount = lowStock.Count();

            // 3. Compute BCG Matrix with In-Place collection update to avoid visual tree re-allocation
            var bcgResult = await _analyticsEngine.ComputeBcgMatrixAsync();
            BcgItems.Clear();
            foreach (var item in bcgResult.Items)
            {
                BcgItems.Add(item);
            }

            // 4. Peak hours analysis
            var peakResult = await _analyticsEngine.ComputePeakHoursAsync();
            PeakHoursSummary = $"Khung giờ cao điểm: {peakResult.PrimaryPeakPeriod}\nKhuyến nghị: {peakResult.StaffingRecommendations}";

            // 5. Inventory forecast
            var forecastResult = await _analyticsEngine.ComputeWeeklyReorderForecastAsync();
            InventoryForecastSummary = forecastResult.AiLogisticsAdvisory;
        }
        catch (Exception ex)
        {
            PeakHoursSummary = $"Lưu ý tải dữ liệu: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
