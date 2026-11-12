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
/// ViewModel driving the touch POS ordering station, cart management, and atomic BOM inventory commitment.
/// </summary>
public class PosOrderViewModel : ViewModelBase
{
    private readonly IMenuService _menuService;
    private readonly IOrderService _orderService;
    private readonly IInventoryService _inventoryService;
    private readonly IAuthService? _authService;

    private DiningTable? _currentTable;
    private List<MenuItem> _allMenuItems = new();
    private ObservableCollection<MenuItem> _displayMenuItems = new();
    private ObservableCollection<MenuCategory> _categories = new();
    private MenuCategory? _selectedCategory;
    private string _searchKeyword = string.Empty;

    private ObservableCollection<CartItemViewModel> _cartItems = new();
    private decimal _subTotal;
    private string? _currentAllergyNote;
    private string _statusMessage = string.Empty;
    private bool _isSuccessMessage;

    public event Action? RequestClosePos;

    public DiningTable? CurrentTable
    {
        get => _currentTable;
        set => SetProperty(ref _currentTable, value);
    }

    public ObservableCollection<MenuItem> DisplayMenuItems
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

    public RelayCommand<MenuItem> AddToCartCommand { get; }
    public RelayCommand<CartItemViewModel> IncreaseQuantityCommand { get; }
    public RelayCommand<CartItemViewModel> DecreaseQuantityCommand { get; }
    public RelayCommand<CartItemViewModel> RemoveFromCartCommand { get; }
    public RelayCommand ClearCartCommand { get; }
    public IAsyncRelayCommand ConfirmOrderCommand { get; }
    public RelayCommand BackCommand { get; }

    public PosOrderViewModel(
        IMenuService menuService,
        IOrderService orderService,
        IInventoryService inventoryService,
        IAuthService? authService = null)
    {
        _menuService = menuService;
        _orderService = orderService;
        _inventoryService = inventoryService;
        _authService = authService;

        AddToCartCommand = new RelayCommand<MenuItem>(AddToCart);
        IncreaseQuantityCommand = new RelayCommand<CartItemViewModel>(IncreaseQuantity);
        DecreaseQuantityCommand = new RelayCommand<CartItemViewModel>(DecreaseQuantity);
        RemoveFromCartCommand = new RelayCommand<CartItemViewModel>(RemoveFromCart);
        ClearCartCommand = new RelayCommand(ClearCart);
        ConfirmOrderCommand = new AsyncRelayCommand(ConfirmOrderAsync);
        BackCommand = new RelayCommand(() => RequestClosePos?.Invoke());
    }

    public async Task InitializeAsync(DiningTable table)
    {
        CurrentTable = table;
        ClearCart();
        StatusMessage = string.Empty;

        var cats = await _menuService.GetAllCategoriesAsync();
        Categories = new ObservableCollection<MenuCategory>(cats);

        var items = await _menuService.GetAvailableMenuItemsAsync();
        _allMenuItems = items.ToList();

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = _allMenuItems.AsEnumerable();

        if (SelectedCategory != null)
        {
            filtered = filtered.Where(m => m.CategoryId == SelectedCategory.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            filtered = filtered.Where(m => m.Name.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase));
        }

        DisplayMenuItems = new ObservableCollection<MenuItem>(filtered);
    }

    private void AddToCart(MenuItem? item)
    {
        if (item == null) return;

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
            CartItems.Remove(cartItem);
            RecalculateTotals();
        }
    }

    private void ClearCart()
    {
        CartItems.Clear();
        RecalculateTotals();
        StatusMessage = string.Empty;
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
