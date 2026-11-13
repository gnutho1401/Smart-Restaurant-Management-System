using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Services;
using SmartRestaurant.WPF.ViewModels;
using Xunit;

namespace SmartRestaurant.Tests;

public class Phase4AiAndStressTests
{
    [Fact]
    public void PosOrderViewModel_SmartUpsell_RecommendsBeverageWhenFoodSelected()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var menuService = new MenuService(context);
        var orderService = new OrderService(context);
        var inventoryService = new InventoryService(context);

        var vm = new PosOrderViewModel(menuService, orderService, inventoryService);

        // Seed food and beverage
        var food = new MenuItem { MenuItemId = 1, Name = "Bò Bít Tết Wagyu", Price = 350000m };
        var drink = new MenuItem { MenuItemId = 2, Name = "Trà Đào Cam Sả", Price = 35000m };

        var posFood = new PosMenuItemViewModel(food, remainingPortions: 10);
        var posDrink = new PosMenuItemViewModel(drink, remainingPortions: 20);

        // Add food to cart
        vm.AddToCart(posFood);

        // Assert food is in cart
        Assert.Single(vm.CartItems);
        Assert.Equal("Bò Bít Tết Wagyu", vm.CartItems[0].DishName);

        // Manually trigger upsell with drink candidate available
        vm.DisplayMenuItems = new System.Collections.ObjectModel.ObservableCollection<PosMenuItemViewModel> { posFood, posDrink };
        vm.TriggerSmartUpsell(posFood);

        // Smart Upsell Banner should recommend the drink
        Assert.True(vm.IsUpsellVisible);
        Assert.NotNull(vm.SuggestedUpsellItem);
        Assert.Equal("Trà Đào Cam Sả", vm.SuggestedUpsellItem.Name);
        Assert.Contains("Trà Đào Cam Sả", vm.UpsellMessage);

        // Accept upsell
        vm.AcceptUpsellCommand.Execute(null);

        // Cart should now contain 2 items (Steak + Drink) and upsell is dismissed
        Assert.Equal(2, vm.CartItems.Count);
        Assert.False(vm.IsUpsellVisible);
        Assert.Equal(385000m, vm.SubTotal); // 350k + 35k
    }

    [Fact]
    public void PosOrderViewModel_CalculateRemainingPortions_ReflectsBomInventory()
    {
        // 1. Dish with 500g Beef remaining, 100g per portion -> 5 portions
        var beef = new Ingredient { IngredientId = 1, Name = "Thịt Bò", CurrentStock = 500m, Unit = "g" };
        var pho = new MenuItem { MenuItemId = 1, Name = "Phở Bò Tái", Price = 60000m };
        pho.Recipes.Add(new Recipe { Ingredient = beef, IngredientId = 1, QuantityRequired = 100m });

        int portions = PosOrderViewModel.CalculateRemainingPortions(pho);
        Assert.Equal(5, portions);

        var posItem = new PosMenuItemViewModel(pho, portions);
        Assert.True(posItem.IsLowStock);
        Assert.False(posItem.IsSoldOut);
        Assert.True(posItem.CanOrder);
        Assert.Equal("🔥 Chỉ còn 5 phần", posItem.StockBadgeText);

        // 2. Dish with 0g stock -> Sold Out
        beef.CurrentStock = 0m;
        int soldOutPortions = PosOrderViewModel.CalculateRemainingPortions(pho);
        Assert.Equal(0, soldOutPortions);

        var soldOutItem = new PosMenuItemViewModel(pho, soldOutPortions);
        Assert.True(soldOutItem.IsSoldOut);
        Assert.False(soldOutItem.CanOrder);
        Assert.Equal("TẠM HẾT HÀNG", soldOutItem.StockBadgeText);
    }

    [Fact]
    public async Task StressTest_100ConcurrentOrders_PreservesBomStockIntegrity()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var inventoryService = new InventoryService(context);
        var orderService = new OrderService(context);

        // Seed 1 shared ingredient: 3,000g of Rice
        var rice = new Ingredient { IngredientId = 1, Name = "Gạo ST25", CurrentStock = 3000m, Unit = "g" };
        context.Ingredients.Add(rice);

        // Seed 1 dish: Fried Rice requiring 100g of Rice per portion
        var dish = new MenuItem { MenuItemId = 1, Name = "Cơm Rang Hải Sản", Price = 75000m, IsAvailable = true };
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var recipe = new Recipe { MenuItemId = dish.MenuItemId, IngredientId = rice.IngredientId, QuantityRequired = 100m };
        context.Recipes.Add(recipe);

        // Seed 100 tables
        for (int i = 1; i <= 100; i++)
        {
            context.DiningTables.Add(new DiningTable { TableId = i, TableName = $"T-{i:D2}", Capacity = 4, Status = TableStatus.Available, AreaId = 1 });
        }
        await context.SaveChangesAsync();

        // 3000g / 100g = Exactly 30 portions available!
        // We simulate 100 concurrent orders: exactly 30 should succeed, and 70 should fail with InsufficientStockException.
        // Invariant: Final CurrentStock must be >= 0 and never drop below 0.
        int successfulOrders = 0;
        int rejectedOrders = 0;
        var syncLock = new object();

        // Create 100 orders first
        var orderIds = new List<int>();
        for (int i = 1; i <= 100; i++)
        {
            var ord = await orderService.CreateOrderAsync(i, 1, new List<(int, int, string?)> { (dish.MenuItemId, 1, null) });
            orderIds.Add(ord.OrderId);
        }

        // Concurrently execute BOM deductions
        var deductionTasks = orderIds.Select(async id =>
        {
            try
            {
                bool deducted = await inventoryService.DeductBomIngredientsAsync(id);
                if (deducted)
                {
                    lock (syncLock) successfulOrders++;
                }
            }
            catch (SmartRestaurant.Core.Exceptions.InsufficientStockException)
            {
                lock (syncLock) rejectedOrders++;
            }
            catch (SmartRestaurant.Core.Exceptions.ConcurrencyConflictException)
            {
                lock (syncLock) rejectedOrders++;
            }
        });

        await Task.WhenAll(deductionTasks);

        // Verify Invariants:
        var updatedRice = await context.Ingredients.FindAsync(rice.IngredientId);
        Assert.NotNull(updatedRice);
        Assert.True(updatedRice.CurrentStock >= 0m, $"Kho nguyên liệu bị âm: {updatedRice.CurrentStock}g");
        Assert.Equal(30, successfulOrders);
        Assert.Equal(70, rejectedOrders);
        Assert.Equal(0m, updatedRice.CurrentStock); // 3000g - (30 * 100g) = 0g
    }
}
