using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class OrderVoidTests
{
    [Fact]
    public async Task VoidOrderItem_PendingStatus_CancelsItemAndRestocksRawIngredients()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var orderService = new OrderService(context);

        // Setup ingredient and recipe: 1 Phở Bò uses 0.2 kg Thịt Bò
        var beef = new Ingredient { Name = "Thịt Bò Tươi", CurrentStock = 10.0m, Unit = "kg", MinStock = 2.0m };
        var dish = new MenuItem { Name = "Phở Bò Đặc Biệt", Price = 75000m, IsAvailable = true };
        var recipe = new Recipe { MenuItem = dish, Ingredient = beef, QuantityRequired = 0.2m };
        dish.Recipes.Add(recipe);

        var table = new DiningTable { TableName = "T-05", Status = TableStatus.Available, Capacity = 4 };
        context.Ingredients.Add(beef);
        context.MenuItems.Add(dish);
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        // Create order for 2 portions -> requires 0.4 kg beef
        var order = await orderService.CreateOrderAsync(table.TableId, 1, new List<(int, int, string?)> { (dish.MenuItemId, 2, null) });
        var item = order.OrderItems.First();

        // Simulate stock deduction at POS submit
        beef.CurrentStock -= (recipe.QuantityRequired * item.Quantity); // Stock goes to 9.6 kg
        await context.SaveChangesAsync();
        Assert.Equal(9.6m, beef.CurrentStock);

        // Void the item while still Pending
        bool voided = await orderService.VoidOrderItemAsync(item.OrderItemId, "Khách đổi sang cơm", actorUserId: 1);
        Assert.True(voided);

        // Item should be marked cancelled with reason
        var updatedItem = context.OrderItems.First(oi => oi.OrderItemId == item.OrderItemId);
        Assert.True(updatedItem.IsCancelled);
        Assert.Equal("Khách đổi sang cơm", updatedItem.CancelReason);

        // Smart BOM return: Raw ingredients should be refunded back to 10.0 kg!
        var updatedBeef = context.Ingredients.First(i => i.IngredientId == beef.IngredientId);
        Assert.Equal(10.0m, updatedBeef.CurrentStock);

        // Order subtotal should be updated to 0
        var updatedOrder = await orderService.GetOrderByIdAsync(order.OrderId);
        Assert.NotNull(updatedOrder);
        Assert.Equal(0m, updatedOrder.SubTotal);
    }

    [Fact]
    public async Task VoidOrderItem_CookingStatus_CancelsItemWithoutRestockingWastedIngredients()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var orderService = new OrderService(context);

        var beef = new Ingredient { Name = "Thịt Bò", CurrentStock = 10.0m, Unit = "kg" };
        var dish = new MenuItem { Name = "Bò Xào", Price = 90000m, IsAvailable = true };
        var recipe = new Recipe { MenuItem = dish, Ingredient = beef, QuantityRequired = 0.3m };
        dish.Recipes.Add(recipe);

        var table = new DiningTable { TableName = "T-06", Status = TableStatus.Available, Capacity = 4 };
        context.Ingredients.Add(beef);
        context.MenuItems.Add(dish);
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var order = await orderService.CreateOrderAsync(table.TableId, 1, new List<(int, int, string?)> { (dish.MenuItemId, 1, null) });
        var item = order.OrderItems.First();

        // Simulate stock deduction and Kitchen starts cooking
        beef.CurrentStock -= 0.3m; // 9.7 kg
        item.CookingStatus = CookingStatus.Cooking;
        await context.SaveChangesAsync();

        // Void the item while Cooking
        bool voided = await orderService.VoidOrderItemAsync(item.OrderItemId, "Khách hủy do đợi lâu", actorUserId: 1);
        Assert.True(voided);

        // Waste policy: ingredients already cooked cannot be restocked
        var updatedBeef = context.Ingredients.First(i => i.IngredientId == beef.IngredientId);
        Assert.Equal(9.7m, updatedBeef.CurrentStock); // Stock NOT refunded
    }
}