using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Exceptions;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// Presentation wrapper for menu item with real-time BOM portion tracking and out-of-stock badges.
/// </summary>
public class PosMenuItemViewModel : ViewModelBase
{
    public MenuItem Item { get; }
    public int MenuItemId => Item.MenuItemId;
    public string Name => Item.Name;
    public decimal Price => Item.Price;
    public int CategoryId => Item.CategoryId;
    public string? CategoryName => Item.Category?.CategoryName;
    public int RemainingPortions { get; set; } = 99;

    public bool IsSoldOut => RemainingPortions <= 0;
    public bool IsLowStock => RemainingPortions > 0 && RemainingPortions <= 5;
    public bool CanOrder => RemainingPortions > 0;

    public string StockBadgeText => IsSoldOut ? "TẠM HẾT HÀNG" : (IsLowStock ? $"🔥 Chỉ còn {RemainingPortions} phần" : string.Empty);
    public string StockBadgeColor => IsSoldOut ? "#EF4444" : "#F59E0B";
    public bool HasStockBadge => !string.IsNullOrEmpty(StockBadgeText);
    public string AddButtonText => IsSoldOut ? "TẠM HẾT HÀNG" : "+ Chọn Món";
    public string AddButtonBackground => IsSoldOut ? "#94A3B8" : "#2980B9";

    public PosMenuItemViewModel(MenuItem item, int remainingPortions = 99)
    {
        Item = item;
        RemainingPortions = remainingPortions;
    }
}

/// <summary>
/// ViewModel driving the touch POS ordering station, cart management, and atomic BOM inventory commitment.
/// </summary>
public class PosOrderViewModel : ViewModelBase
{
    private readonly IMenuService _menuService;
    private readonly IOrderService _orderService;
    private readonly IInventoryService _inventoryService;
    private readonly IAuthService? _authService;
    private readonly SmartRestaurant.AIEngine.AiAnalyticsEngine? _analyticsEngine;

    private DiningTable? _currentTable;
    private List<PosMenuItemViewModel> _allPosItems = new();
    private ObservableCollection<PosMenuItemViewModel> _displayMenuItems = new();
    private ObservableCollection<MenuCategory> _categories = new();
    private MenuCategory? _selectedCategory;
    private string _searchKeyword = string.Empty;

    private ObservableCollection<CartItemViewModel> _cartItems = new();
    private decimal _subTotal;
    private string? _currentAllergyNote;
    private string _statusMessage = string.Empty;
    private bool _isSuccessMessage;

    // AI Smart Upsell recommendation properties
    private bool _isUpsellVisible;
    private string _upsellMessage = string.Empty;
    private PosMenuItemViewModel? _suggestedUpsellItem;

    public bool IsUpsellVisible
    {
        get => _isUpsellVisible;
        set => SetProperty(ref _isUpsellVisible, value);
    }

    public string UpsellMessage
    {
        get => _upsellMessage;
        set => SetProperty(ref _upsellMessage, value);
    }

    public PosMenuItemViewModel? SuggestedUpsellItem
    {
        get => _suggestedUpsellItem;
        set => SetProperty(ref _suggestedUpsellItem, value);
    }

    public event Action? RequestClosePos;

    public DiningTable? CurrentTable
    {
        get => _currentTable;
        set => SetProperty(ref _currentTable, value);
    }

    public ObservableCollection<PosMenuItemViewModel> DisplayMenuItems
    {
        get => _displayMenuItems;
        set => SetProperty(ref _displayMenuItems, value);
    }

    public ObservableCollection<MenuCategory> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    public MenuCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SearchKeyword
    {
        get => _searchKeyword;
        set
        {
            if (SetProperty(ref _searchKeyword, value))
            {
                ApplyFilter();
            }
        }
    }

    public ObservableCollection<CartItemViewModel> CartItems
    {
        get => _cartItems;
        set => SetProperty(ref _cartItems, value);
    }

    public decimal SubTotal
    {
        get => _subTotal;
        set => SetProperty(ref _subTotal, value);
    }

    public string? CurrentAllergyNote
    {
        get => _currentAllergyNote;
        set => SetProperty(ref _currentAllergyNote, value);
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

    private bool _isToastVisible;
    private string _toastMessage = string.Empty;
    private Action? _undoAction;
    private System.Threading.CancellationTokenSource? _toastCts;

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

    public RelayCommand<object> AddToCartCommand { get; }
    public RelayCommand<CartItemViewModel> IncreaseQuantityCommand { get; }
    public RelayCommand<CartItemViewModel> DecreaseQuantityCommand { get; }
    public RelayCommand<CartItemViewModel> RemoveFromCartCommand { get; }
    public RelayCommand ClearCartCommand { get; }
    public IAsyncRelayCommand ConfirmOrderCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand UndoToastCommand { get; }
    public RelayCommand AcceptUpsellCommand { get; }
    public RelayCommand DismissUpsellCommand { get; }

    public PosOrderViewModel(
        IMenuService menuService,
        IOrderService orderService,
        IInventoryService inventoryService,
        IAuthService? authService = null,
        SmartRestaurant.AIEngine.AiAnalyticsEngine? analyticsEngine = null)
    {
        _menuService = menuService;
        _orderService = orderService;
        _inventoryService = inventoryService;
        _authService = authService;
        _analyticsEngine = analyticsEngine;

        AddToCartCommand = new RelayCommand<object>(AddToCart);
        IncreaseQuantityCommand = new RelayCommand<CartItemViewModel>(IncreaseQuantity);
        DecreaseQuantityCommand = new RelayCommand<CartItemViewModel>(DecreaseQuantity);
        RemoveFromCartCommand = new RelayCommand<CartItemViewModel>(RemoveFromCart);
        ClearCartCommand = new RelayCommand(ExecuteClearCartUser);
        ConfirmOrderCommand = new AsyncRelayCommand(ConfirmOrderAsync);
        BackCommand = new RelayCommand(() => RequestClosePos?.Invoke());
        UndoToastCommand = new RelayCommand(ExecuteUndoToast);
        AcceptUpsellCommand = new RelayCommand(AcceptUpsell);
        DismissUpsellCommand = new RelayCommand(DismissUpsell);
    }

    public async Task InitializeAsync(DiningTable table)
    {
        CurrentTable = table;
        ClearCart();
        StatusMessage = string.Empty;
        IsUpsellVisible = false;

        var cats = await _menuService.GetAllCategoriesAsync();
        Categories = new ObservableCollection<MenuCategory>(cats);

        var items = await _menuService.GetAvailableMenuItemsAsync();
        _allPosItems = items.Select(m => new PosMenuItemViewModel(m, CalculateRemainingPortions(m))).ToList();

        ApplyFilter();
    }

    public static int CalculateRemainingPortions(MenuItem item)
    {
        if (item.Recipes == null || !item.Recipes.Any())
        {
            return 99; // Non-BOM or unconstrained
        }

        int minPortions = int.MaxValue;
        foreach (var recipe in item.Recipes)
        {
            if (recipe.Ingredient != null && recipe.QuantityRequired > 0)
            {
                int portions = (int)Math.Floor(recipe.Ingredient.CurrentStock / recipe.QuantityRequired);
                if (portions < minPortions)
                {
                    minPortions = portions;
                }
            }
        }

        return minPortions == int.MaxValue ? 99 : Math.Max(0, minPortions);
    }

    private void ApplyFilter()
    {
        var filtered = _allPosItems.AsEnumerable();

        if (SelectedCategory != null)
        {
            filtered = filtered.Where(m => m.CategoryId == SelectedCategory.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            filtered = filtered.Where(m => m.Name.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase));
        }

        DisplayMenuItems = new ObservableCollection<PosMenuItemViewModel>(filtered);
    }

    public void AddToCart(object? param)
    {
        MenuItem? item = null;
        PosMenuItemViewModel? posItem = null;

        if (param is PosMenuItemViewModel p)
        {
            posItem = p;
            item = p.Item;
        }
        else if (param is MenuItem m)
        {
            item = m;
            posItem = _allPosItems.FirstOrDefault(x => x.MenuItemId == m.MenuItemId);
        }

        if (item == null) return;
        if (posItem != null && posItem.IsSoldOut) return;

        var existing = CartItems.FirstOrDefault(c => c.MenuItemId == item.MenuItemId);
        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            CartItems.Add(new CartItemViewModel
            {
                MenuItemId = item.MenuItemId,
                DishName = item.Name,
                UnitPrice = item.Price,
                Quantity = 1,
                AllergyNote = CurrentAllergyNote
            });
            CurrentAllergyNote = string.Empty; // Reset input field after adding
        }

        RecalculateTotals();

        // Trigger AI Smart Upsell
        TriggerSmartUpsell(posItem ?? new PosMenuItemViewModel(item));
    }

    public async void TriggerSmartUpsell(PosMenuItemViewModel selectedItem)
    {
        if (selectedItem == null) return;

        IEnumerable<PosMenuItemViewModel> itemsSource = _allPosItems.Any() ? _allPosItems : DisplayMenuItems;

        // 1. Data-Driven Market Basket Analysis (Apriori Rule Mining: Lift >= 1.0)
        if (_analyticsEngine != null && selectedItem.Item != null)
        {
            try
            {
                var rule = await _analyticsEngine.GetSmartUpsellRecommendationAsync(
                    selectedItem.Item.MenuItemId,
                    CartItems.Select(c => c.MenuItemId));

                if (rule != null)
                {
                    var match = itemsSource.FirstOrDefault(m => m.MenuItemId == rule.SuggestedDishId && !m.IsSoldOut);
                    if (match != null)
                    {
                        SuggestedUpsellItem = match;
                        UpsellMessage = rule.MarketingPitch;
                        IsUpsellVisible = true;
                        return;
                    }
                }
            }
            catch
            {
                // Fallback gracefully to domain heuristics if query encounters errors
            }
        }

        // 2. High-margin Beverage/Dessert Domain Heuristics Fallback
        bool isDrinkOrDessert = (selectedItem.CategoryName?.Contains("Uống", StringComparison.OrdinalIgnoreCase) == true) ||
                                (selectedItem.CategoryName?.Contains("Tráng miệng", StringComparison.OrdinalIgnoreCase) == true) ||
                                selectedItem.Name.Contains("Nước", StringComparison.OrdinalIgnoreCase) ||
                                selectedItem.Name.Contains("Trà", StringComparison.OrdinalIgnoreCase) ||
                                selectedItem.Name.Contains("Rượu", StringComparison.OrdinalIgnoreCase) ||
                                selectedItem.Name.Contains("Bia", StringComparison.OrdinalIgnoreCase);

        bool isFood = !isDrinkOrDessert;

        if (isFood)
        {
            var candidate = itemsSource.FirstOrDefault(m =>
                !m.IsSoldOut &&
                !CartItems.Any(c => c.MenuItemId == m.MenuItemId) &&
                ((m.CategoryName?.Contains("Uống", StringComparison.OrdinalIgnoreCase) == true) ||
                 (m.CategoryName?.Contains("Tráng miệng", StringComparison.OrdinalIgnoreCase) == true) ||
                 m.Name.Contains("Trà", StringComparison.OrdinalIgnoreCase) ||
                 m.Name.Contains("Nước", StringComparison.OrdinalIgnoreCase) ||
                 m.Name.Contains("Rượu", StringComparison.OrdinalIgnoreCase) ||
                 m.Name.Contains("Bia", StringComparison.OrdinalIgnoreCase)));

            if (candidate != null)
            {
                SuggestedUpsellItem = candidate;
                UpsellMessage = $"💡 Gợi ý kết hợp hoàn hảo: Thêm {candidate.Name} (+{candidate.Price:N0} đ)?";
                IsUpsellVisible = true;
                return;
            }
        }

        IsUpsellVisible = false;
    }

    private void AcceptUpsell()
    {
        if (SuggestedUpsellItem != null)
        {
            AddToCart(SuggestedUpsellItem);
            IsUpsellVisible = false;
            SuggestedUpsellItem = null;
        }
    }

    private void DismissUpsell()
    {
        IsUpsellVisible = false;
        SuggestedUpsellItem = null;
    }

    private void IncreaseQuantity(CartItemViewModel? cartItem)
    {
        if (cartItem == null) return;
        cartItem.Quantity++;
        RecalculateTotals();
    }

    private void DecreaseQuantity(CartItemViewModel? cartItem)
    {
        if (cartItem == null) return;
        if (cartItem.Quantity > 1)
        {
            cartItem.Quantity--;
        }
        else
        {
            CartItems.Remove(cartItem);
        }
        RecalculateTotals();
    }

    private void RemoveFromCart(CartItemViewModel? cartItem)
    {
        if (cartItem != null)
        {
            var index = CartItems.IndexOf(cartItem);
            CartItems.Remove(cartItem);
            RecalculateTotals();

            ShowToastWithUndo($"Đã xóa món {cartItem.DishName} khỏi giỏ.", () =>
            {
                if (index >= 0 && index <= CartItems.Count)
                {
                    CartItems.Insert(index, cartItem);
                }
                else
                {
                    CartItems.Add(cartItem);
                }
                RecalculateTotals();
            });
        }
    }

    private void ClearCart()
    {
        CartItems.Clear();
        RecalculateTotals();
        StatusMessage = string.Empty;
    }

    private void ExecuteClearCartUser()
    {
        if (!CartItems.Any()) return;
        var backup = CartItems.ToList();
        ClearCart();
        ShowToastWithUndo("Đã xóa toàn bộ giỏ món ăn.", () =>
        {
            foreach (var item in backup)
            {
                CartItems.Add(item);
            }
            RecalculateTotals();
        });
    }

    public void ShowToastWithUndo(string message, Action undoAction)
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

    public void ExecuteUndoToast()
    {
        _toastCts?.Cancel();
        IsToastVisible = false;

        if (_undoAction != null)
        {
            var action = _undoAction;
            _undoAction = null;
            action.Invoke();
        }
    }

    private void RecalculateTotals()
    {
        SubTotal = CartItems.Sum(c => c.LineTotal);
    }

    private async Task ConfirmOrderAsync()
    {
        if (CurrentTable == null) return;
        if (!CartItems.Any())
        {
            StatusMessage = "Vui lòng chọn ít nhất một món ăn trước khi xác nhận.";
            IsSuccessMessage = false;
            return;
        }

        try
        {
            // 1. Prepare Items list
            var items = CartItems
                .Select(c => (c.MenuItemId, c.Quantity, c.AllergyNote))
                .ToList();

            // 2. Create Order in DB
            int userId = _authService?.CurrentUser?.UserId ?? 1;
            var order = await _orderService.CreateOrderAsync(CurrentTable.TableId, userId, items);

            // 3. Atomic BOM Inventory Deduction with Optimistic Locking
            await _inventoryService.DeductBomIngredientsAsync(order.OrderId);

            StatusMessage = $"✅ Đã gửi đơn #{order.OrderId:D4} vào bếp và trừ kho BOM thành công!";
            IsSuccessMessage = true;

            ClearCart();
        }
        catch (InsufficientStockException ex)
        {
            StatusMessage = $"❌ Không đủ kho nguyên liệu: {ex.Message}";
            IsSuccessMessage = false;
        }
        catch (ConcurrencyConflictException ex)
        {
            StatusMessage = $"⚠️ Xung đột kho đồng thời: {ex.Message}";
            IsSuccessMessage = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi gọi món: {ex.Message}";
            IsSuccessMessage = false;
        }
    }
}
