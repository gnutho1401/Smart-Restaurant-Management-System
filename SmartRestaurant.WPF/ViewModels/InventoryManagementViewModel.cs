using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

public class IngredientItemViewModel : ViewModelBase
{
    private decimal _currentStock;

    public int IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "g";
    public decimal MinStock { get; set; }

    public decimal CurrentStock
    {
        get => _currentStock;
        set
        {
            if (SetProperty(ref _currentStock, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    public string StatusText
    {
        get
        {
            if (CurrentStock <= 0) return "CẠN KIỆT";
            if (CurrentStock <= MinStock) return "CẢNH BÁO THIẾU";
            return "AN TOÀN";
        }
    }

    public string StatusColor
    {
        get
        {
            if (CurrentStock <= 0) return "#E74C3C";      // Red
            if (CurrentStock <= MinStock) return "#E67E22"; // Orange
            return "#27AE60";                              // Green
        }
    }
}

/// <summary>
/// ViewModel managing restaurant raw materials, current stock levels, and inbound restocking.
/// </summary>
public class InventoryManagementViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    private readonly SmartRestaurant.AIEngine.AiAnalyticsEngine? _analyticsEngine;

    private ObservableCollection<IngredientItemViewModel> _ingredients = new();
    private IngredientItemViewModel? _selectedIngredient;
    private decimal _restockQuantity = 1000m;
    private string _statusMessage = string.Empty;
    private bool _isSuccessMessage;
    private bool _isBusy;

    public ObservableCollection<IngredientItemViewModel> Ingredients
    {
        get => _ingredients;
        set => SetProperty(ref _ingredients, value);
    }

    public IngredientItemViewModel? SelectedIngredient
    {
        get => _selectedIngredient;
        set => SetProperty(ref _selectedIngredient, value);
    }

    public decimal RestockQuantity
    {
        get => _restockQuantity;
        set => SetProperty(ref _restockQuantity, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSuccessMessage
    {
        get => _isSuccessMessage;
        set => SetProperty(ref _isSuccessMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public IAsyncRelayCommand LoadIngredientsCommand { get; }
    public IAsyncRelayCommand RestockCommand { get; }
    public IAsyncRelayCommand AutoRestockFromAiCommand { get; }

    public InventoryManagementViewModel(
        IInventoryService inventoryService,
        SmartRestaurant.AIEngine.AiAnalyticsEngine? analyticsEngine = null)
    {
        _inventoryService = inventoryService;
        _analyticsEngine = analyticsEngine;

        LoadIngredientsCommand = new AsyncRelayCommand(LoadIngredientsAsync);
        RestockCommand = new AsyncRelayCommand(RestockAsync);
        AutoRestockFromAiCommand = new AsyncRelayCommand(AutoRestockFromAiAsync);
    }

    public async Task LoadIngredientsAsync()
    {
        IsBusy = true;
        try
        {
            var raw = await _inventoryService.GetAllIngredientsAsync();
            var items = raw.Select(i => new IngredientItemViewModel
            {
                IngredientId = i.IngredientId,
                Name = i.Name,
                Unit = i.Unit,
                CurrentStock = i.CurrentStock,
                MinStock = i.MinStock
            }).ToList();

            Ingredients = new ObservableCollection<IngredientItemViewModel>(items);
            if (SelectedIngredient == null && Ingredients.Any())
            {
                SelectedIngredient = Ingredients.First();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi tải kho: {ex.Message}";
            IsSuccessMessage = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestockAsync()
    {
        if (SelectedIngredient == null)
        {
            StatusMessage = "Vui lòng chọn nguyên liệu cần nhập kho.";
            IsSuccessMessage = false;
            return;
        }

        if (RestockQuantity <= 0)
        {
            StatusMessage = "Số lượng nhập kho phải lớn hơn 0.";
            IsSuccessMessage = false;
            return;
        }

        IsBusy = true;
        try
        {
            var success = await _inventoryService.RestockIngredientAsync(SelectedIngredient.IngredientId, RestockQuantity);
            if (success)
            {
                SelectedIngredient.CurrentStock += RestockQuantity;
                StatusMessage = $"Đã nhập kho thành công +{RestockQuantity:N0} {SelectedIngredient.Unit} cho '{SelectedIngredient.Name}'!";
                IsSuccessMessage = true;
            }
            else
            {
                StatusMessage = "Không tìm thấy nguyên liệu để cập nhật.";
                IsSuccessMessage = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi nhập kho: {ex.Message}";
            IsSuccessMessage = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AutoRestockFromAiAsync()
    {
        if (_analyticsEngine == null)
        {
            StatusMessage = "AI Analytics Engine chưa được khởi tạo.";
            IsSuccessMessage = false;
            return;
        }

        IsBusy = true;
        StatusMessage = "Đang phân tích tồn kho và ngưỡng an toàn chuỗi cung ứng...";
        IsSuccessMessage = true;

        try
        {
            var forecast = await _analyticsEngine.ComputeWeeklyReorderForecastAsync();
            if (forecast.PurchaseOrderDraft == null || !forecast.PurchaseOrderDraft.Any())
            {
                StatusMessage = "Tồn kho toàn bộ nguyên vật liệu hiện đang ở mức an toàn. Chưa cần nhập thêm.";
                IsSuccessMessage = true;
                return;
            }

            int count = await _inventoryService.ApplyPurchaseOrderDraftAsync(forecast.PurchaseOrderDraft);
            await LoadIngredientsAsync();

            StatusMessage = $"AI đã tự động tạo phiếu PO và nhập kho thành công {count} nguyên vật liệu cảnh báo!";
            IsSuccessMessage = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi tự động nhập kho AI: {ex.Message}";
            IsSuccessMessage = false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

