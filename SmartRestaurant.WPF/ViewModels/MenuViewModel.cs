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
/// ViewModel managing restaurant menu catalog, dynamic pricing, and Bill-of-Materials (BOM) formulation.
/// Tightly integrates with Local GenAI to synthesize mouth-watering culinary descriptions.
/// </summary>
public class MenuViewModel : ViewModelBase
{
    private readonly IMenuService _menuService;
    private readonly IInventoryService _inventoryService;
    private readonly IAiService _aiService;
    private readonly IAuthService? _authService;

    private ObservableCollection<MenuItem> _menuItems = new();
    private MenuItem? _selectedDish;
    private ObservableCollection<MenuCategory> _categories = new();
    private ObservableCollection<Ingredient> _availableIngredients = new();

    // Editor Form Fields
    private int _editingDishId;
    private string _dishName = string.Empty;
    private decimal _dishPrice;
    private int _selectedCategoryId;
    private string? _dishDescription;
    private bool _dishIsAvailable = true;

    // BOM Formulation items
    private ObservableCollection<RecipeItemViewModel> _recipeIngredients = new();
    private Ingredient? _selectedIngredientToAdd;
    private decimal _quantityToAdd = 50;

    private string _statusMessage = string.Empty;
    private bool _isGeneratingAiDescription;

    public ObservableCollection<MenuItem> MenuItems
    {
        get => _menuItems;
        set => SetProperty(ref _menuItems, value);
    }

    public MenuItem? SelectedDish
    {
        get => _selectedDish;
        set
        {
            if (SetProperty(ref _selectedDish, value) && value != null)
            {
                LoadDishIntoForm(value);
            }
        }
    }

    public ObservableCollection<MenuCategory> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    public ObservableCollection<Ingredient> AvailableIngredients
    {
        get => _availableIngredients;
        set => SetProperty(ref _availableIngredients, value);
    }

    public string DishName
    {
        get => _dishName;
        set => SetProperty(ref _dishName, value);
    }

    public decimal DishPrice
    {
        get => _dishPrice;
        set => SetProperty(ref _dishPrice, value);
    }

    public int SelectedCategoryId
    {
        get => _selectedCategoryId;
        set => SetProperty(ref _selectedCategoryId, value);
    }

    public string? DishDescription
    {
        get => _dishDescription;
        set => SetProperty(ref _dishDescription, value);
    }

    public bool DishIsAvailable
    {
        get => _dishIsAvailable;
        set => SetProperty(ref _dishIsAvailable, value);
    }

    public ObservableCollection<RecipeItemViewModel> RecipeIngredients
    {
        get => _recipeIngredients;
        set => SetProperty(ref _recipeIngredients, value);
    }

    public Ingredient? SelectedIngredientToAdd
    {
        get => _selectedIngredientToAdd;
        set => SetProperty(ref _selectedIngredientToAdd, value);
    }

    public decimal QuantityToAdd
    {
        get => _quantityToAdd;
        set => SetProperty(ref _quantityToAdd, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsGeneratingAiDescription
    {
        get => _isGeneratingAiDescription;
        set => SetProperty(ref _isGeneratingAiDescription, value);
    }

    public IAsyncRelayCommand LoadMenuCommand { get; }
    public RelayCommand NewDishCommand { get; }
    public IAsyncRelayCommand SaveDishCommand { get; }
    public IAsyncRelayCommand DeleteDishCommand { get; }
    public RelayCommand AddIngredientCommand { get; }
    public RelayCommand<RecipeItemViewModel> RemoveIngredientCommand { get; }
    public IAsyncRelayCommand GenerateAiDescriptionCommand { get; }

    public bool CanEditPrice => _authService?.MeetsMinimumRole(UserRole.StoreManager) ?? true;

    public MenuViewModel(
        IMenuService menuService,
        IInventoryService inventoryService,
        IAiService aiService,
        IAuthService? authService = null)
    {
        _menuService = menuService;
        _inventoryService = inventoryService;
        _aiService = aiService;
        _authService = authService;

        LoadMenuCommand = new AsyncRelayCommand(LoadMenuAsync);
        NewDishCommand = new RelayCommand(ClearFormForNewDish);
        SaveDishCommand = new AsyncRelayCommand(SaveDishAsync);
        DeleteDishCommand = new AsyncRelayCommand(DeleteDishAsync);
        AddIngredientCommand = new RelayCommand(AddIngredientToRecipe);
        RemoveIngredientCommand = new RelayCommand<RecipeItemViewModel>(RemoveIngredientFromRecipe);
        GenerateAiDescriptionCommand = new AsyncRelayCommand(GenerateAiDescriptionAsync);
    }

    public async Task LoadMenuAsync()
    {
        var items = await _menuService.GetAllMenuItemsAsync();
        MenuItems = new ObservableCollection<MenuItem>(items);

        var cats = await _menuService.GetAllCategoriesAsync();
        Categories = new ObservableCollection<MenuCategory>(cats);

        var ingredients = await _inventoryService.GetAllIngredientsAsync();
        AvailableIngredients = new ObservableCollection<Ingredient>(ingredients);

        if (MenuItems.Any() && SelectedDish == null)
        {
            SelectedDish = MenuItems.First();
        }
    }

    private void LoadDishIntoForm(MenuItem dish)
    {
        _editingDishId = dish.MenuItemId;
        DishName = dish.Name;
        DishPrice = dish.Price;
        SelectedCategoryId = dish.CategoryId;
        DishDescription = dish.Description;
        DishIsAvailable = dish.IsAvailable;

        var recipeVms = dish.Recipes.Select(r => new RecipeItemViewModel
        {
            IngredientId = r.IngredientId,
            IngredientName = r.Ingredient?.Name ?? $"Nguyên liệu #{r.IngredientId}",
            Unit = r.Ingredient?.Unit ?? "g",
            QuantityRequired = r.QuantityRequired
        });

        RecipeIngredients = new ObservableCollection<RecipeItemViewModel>(recipeVms);
        StatusMessage = string.Empty;
    }

    private void ClearFormForNewDish()
    {
        _editingDishId = 0;
        DishName = string.Empty;
        DishPrice = 100000;
        SelectedCategoryId = Categories.FirstOrDefault()?.CategoryId ?? 1;
        DishDescription = string.Empty;
        DishIsAvailable = true;
        RecipeIngredients.Clear();
        SelectedDish = null;
        StatusMessage = "Nhập thông tin món mới và thêm định lượng BOM.";
    }

    private void AddIngredientToRecipe()
    {
        if (SelectedIngredientToAdd == null || QuantityToAdd <= 0) return;

        var existing = RecipeIngredients.FirstOrDefault(r => r.IngredientId == SelectedIngredientToAdd.IngredientId);
        if (existing != null)
        {
            existing.QuantityRequired += QuantityToAdd;
        }
        else
        {
            RecipeIngredients.Add(new RecipeItemViewModel
            {
                IngredientId = SelectedIngredientToAdd.IngredientId,
                IngredientName = SelectedIngredientToAdd.Name,
                Unit = SelectedIngredientToAdd.Unit,
                QuantityRequired = QuantityToAdd
            });
        }
    }

    private void RemoveIngredientFromRecipe(RecipeItemViewModel? item)
    {
        if (item != null)
        {
            RecipeIngredients.Remove(item);
        }
    }

    private async Task SaveDishAsync()
    {
        if (string.IsNullOrWhiteSpace(DishName))
        {
            StatusMessage = "Vui lòng nhập tên món ăn.";
            return;
        }

        if (DishPrice < 0)
        {
            StatusMessage = "Giá món ăn không thể là số âm.";
            return;
        }

        // Subordinate check: Chef cannot change commercial selling prices without Manager/Admin approval
        if (!CanEditPrice && SelectedDish != null && SelectedDish.Price != DishPrice)
        {
            StatusMessage = "⚠️ Bếp trưởng chỉ có quyền điều chỉnh công thức định lượng BOM. Giá bán thương mại phải do Quản Lý hoặc Admin duyệt.";
            DishPrice = SelectedDish.Price;
        }

        var dish = new MenuItem
        {
            MenuItemId = _editingDishId,
            Name = DishName.Trim(),
            Price = DishPrice,
            CategoryId = SelectedCategoryId,
            Description = DishDescription,
            IsAvailable = DishIsAvailable,
            Recipes = RecipeIngredients.Select(r => new Recipe
            {
                IngredientId = r.IngredientId,
                QuantityRequired = r.QuantityRequired
            }).ToList()
        };

        await _menuService.UpsertMenuItemAsync(dish);
        StatusMessage = $"✅ Đã lưu món '{dish.Name}' và cấu hình BOM thành công!";

        await LoadMenuAsync();
    }

    private async Task DeleteDishAsync()
    {
        if (_editingDishId == 0) return;

        await _menuService.DeleteMenuItemAsync(_editingDishId);
        StatusMessage = "✅ Đã gỡ món khỏi thực đơn bán hàng.";
        await LoadMenuAsync();
    }

    private async Task GenerateAiDescriptionAsync()
    {
        if (string.IsNullOrWhiteSpace(DishName))
        {
            StatusMessage = "Vui lòng nhập tên món trước khi sinh mô tả AI.";
            return;
        }

        IsGeneratingAiDescription = true;
        StatusMessage = "Đang gửi yêu cầu đến mô hình AI...";

        try
        {
            var ingredientNames = RecipeIngredients.Select(r => r.IngredientName).ToList();
            var desc = await _aiService.GenerateDishDescriptionAsync(DishName, ingredientNames);
            DishDescription = desc;
            StatusMessage = "Đã sinh mô tả AI ẩm thực thành công!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi kết nối AI: {ex.Message}";
        }
        finally
        {
            IsGeneratingAiDescription = false;
        }
    }
}
