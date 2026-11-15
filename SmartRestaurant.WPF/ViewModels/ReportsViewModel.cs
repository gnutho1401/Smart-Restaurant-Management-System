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
/// Implements on-demand AI execution so the admin page loads instantly without automatic AI delay.
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
    private string _peakHoursSummary = "Chưa kích hoạt. Quản lý vui lòng nhấn 'Phân Tích AI' để tổng hợp khung giờ cao điểm và khuyến nghị điều phối.";
    private string _inventoryForecastSummary = "Chưa kích hoạt. Quản lý vui lòng nhấn 'Dự Báo AI' để kích hoạt mô hình GenAI dự báo chuỗi cung ứng.";
    private bool _isLoading;
    private bool _isAiRunning;

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

    public bool IsAiRunning
    {
        get => _isAiRunning;
        set => SetProperty(ref _isAiRunning, value);
    }

    public ObservableCollection<BcgItem> BcgItems
    {
        get => _bcgItems;
        set => SetProperty(ref _bcgItems, value);
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand RunBcgCommand { get; }
    public IAsyncRelayCommand RunPeakHoursCommand { get; }
    public IAsyncRelayCommand RunInventoryForecastCommand { get; }
    public IAsyncRelayCommand RunAllAiAnalyticsCommand { get; }

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
        RunBcgCommand = new AsyncRelayCommand(RunBcgAnalysisAsync);
        RunPeakHoursCommand = new AsyncRelayCommand(RunPeakHoursAnalysisAsync);
        RunInventoryForecastCommand = new AsyncRelayCommand(RunInventoryForecastAnalysisAsync);
        RunAllAiAnalyticsCommand = new AsyncRelayCommand(RunAllAiAnalyticsAsync);
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

            // Note: Heavy AI analytics are on-demand per admin request to avoid UI blocking on navigation.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReportsViewModel] LoadDashboard error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RunBcgAnalysisAsync()
    {
        IsAiRunning = true;
        try
        {
            var bcgResult = await _analyticsEngine.ComputeBcgMatrixAsync();
            BcgItems.Clear();
            foreach (var item in bcgResult.Items)
            {
                BcgItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RunBcgAnalysisAsync] Error: {ex.Message}");
        }
        finally
        {
            IsAiRunning = false;
        }
    }

    public async Task RunPeakHoursAnalysisAsync()
    {
        PeakHoursSummary = "Đang tổng hợp dữ liệu giờ cao điểm và phân tích...";
        IsAiRunning = true;
        try
        {
            var peakResult = await _analyticsEngine.ComputePeakHoursAsync();
            PeakHoursSummary = $"Khung giờ cao điểm: {peakResult.PrimaryPeakPeriod}\nKhuyến nghị: {peakResult.StaffingRecommendations}";
        }
        catch (Exception ex)
        {
            PeakHoursSummary = $"Lỗi phân tích giờ cao điểm: {ex.Message}";
        }
        finally
        {
            IsAiRunning = false;
        }
    }

    public async Task RunInventoryForecastAnalysisAsync()
    {
        InventoryForecastSummary = "Đang kết nối mô hình GenAI dự báo chuỗi cung ứng 7 ngày...";
        IsAiRunning = true;
        try
        {
            var forecastResult = await _analyticsEngine.ComputeWeeklyReorderForecastAsync();
            InventoryForecastSummary = forecastResult.AiLogisticsAdvisory;
        }
        catch (Exception ex)
        {
            InventoryForecastSummary = $"Lỗi dự báo kho: {ex.Message}";
        }
        finally
        {
            IsAiRunning = false;
        }
    }

    public async Task RunAllAiAnalyticsAsync()
    {
        await RunBcgAnalysisAsync();
        await RunPeakHoursAnalysisAsync();
        await RunInventoryForecastAnalysisAsync();
    }
}
