using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Exceptions;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class InventoryServiceTests
{
    [Fact]
    public async Task DeductBOM_SufficientStock_DecrementsAccurately()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var beef = new Ingredient { Name = "Thịt Bò", Unit = "kg", CurrentStock = 10.0m, MinStock = 2.0m };
        var onion = new Ingredient { Name = "Hành Tây", Unit = "kg", CurrentStock = 5.0m, MinStock = 1.0m };
        context.Ingredients.AddRange(beef, onion);
        await context.SaveChangesAsync();

        var dish = new MenuItem { Name = "Bò Xào", Price = 120000m, IsAvailable = true };
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var recipe1 = new Recipe { MenuItemId = dish.MenuItemId, IngredientId = beef.IngredientId, QuantityRequired = 0.15m };
        var recipe2 = new Recipe { MenuItemId = dish.MenuItemId, IngredientId = onion.IngredientId, QuantityRequired = 0.05m };
        context.Recipes.AddRange(recipe1, recipe2);

        var order = new Order { TableId = 1, Status = "New" };
        order.OrderItems.Add(new OrderItem { MenuItemId = dish.MenuItemId, Quantity = 2, UnitPrice = 120000m });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new InventoryService(context);

        // Act
        bool result = await service.DeductBomIngredientsAsync(order.OrderId);

        // Assert
        Assert.True(result);
        var updatedBeef = await context.Ingredients.FindAsync(beef.IngredientId);
        var updatedOnion = await context.Ingredients.FindAsync(onion.IngredientId);

        // 10.0 - (0.15 * 2) = 9.70
        Assert.Equal(9.70m, updatedBeef!.CurrentStock);
        // 5.0 - (0.05 * 2) = 4.90
        Assert.Equal(4.90m, updatedOnion!.CurrentStock);
    }

    [Fact]
    public async Task DeductBOM_InsufficientStock_RollsBackTransaction()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var wagyu = new Ingredient { Name = "Bò Wagyu A5", Unit = "kg", CurrentStock = 0.2m, MinStock = 1.0m };
        context.Ingredients.Add(wagyu);
        await context.SaveChangesAsync();

        var dish = new MenuItem { Name = "Steak Wagyu", Price = 950000m, IsAvailable = true };
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        context.Recipes.Add(new Recipe { MenuItemId = dish.MenuItemId, IngredientId = wagyu.IngredientId, QuantityRequired = 0.5m });

        var order = new Order { TableId = 1, Status = "New" };
        order.OrderItems.Add(new OrderItem { MenuItemId = dish.MenuItemId, Quantity = 1, UnitPrice = 950000m });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new InventoryService(context);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InsufficientStockException>(() => service.DeductBomIngredientsAsync(order.OrderId));
        Assert.Equal(wagyu.IngredientId, ex.IngredientId);

        // Verify stock was not deducted (remains 0.2)
        var verifyWagyu = await context.Ingredients.FindAsync(wagyu.IngredientId);
        Assert.Equal(0.2m, verifyWagyu!.CurrentStock);
    }

    [Fact]
    public async Task DeductBOM_MultiplePortions_ScalesProportionally()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var rice = new Ingredient { Name = "Gạo ST25", Unit = "kg", CurrentStock = 2.0m, MinStock = 0.5m };
        context.Ingredients.Add(rice);
        await context.SaveChangesAsync();

        var dish = new MenuItem { Name = "Cơm Chiên Hải Sản", Price = 85000m, IsAvailable = true };
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        context.Recipes.Add(new Recipe { MenuItemId = dish.MenuItemId, IngredientId = rice.IngredientId, QuantityRequired = 0.2m });

        var order = new Order { TableId = 1, Status = "New" };
        order.OrderItems.Add(new OrderItem { MenuItemId = dish.MenuItemId, Quantity = 5, UnitPrice = 85000m });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new InventoryService(context);

        // Act
        bool result = await service.DeductBomIngredientsAsync(order.OrderId);

        // Assert
        Assert.True(result);
        var updatedRice = await context.Ingredients.FindAsync(rice.IngredientId);
        // 2.0 - (5 * 0.2) = 1.0
        Assert.Equal(1.0m, updatedRice!.CurrentStock);
    }

    [Fact]
    public async Task RestockIngredient_PositiveQuantity_IncrementsStock()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var salmon = new Ingredient { Name = "Cá Hồi Nauy", Unit = "kg", CurrentStock = 5.0m, MinStock = 2.0m };
        context.Ingredients.Add(salmon);
        await context.SaveChangesAsync();

        var service = new InventoryService(context);

        // Act
        bool success = await service.RestockIngredientAsync(salmon.IngredientId, 10.0m);

        // Assert
        Assert.True(success);
        var updated = await context.Ingredients.FindAsync(salmon.IngredientId);
        Assert.Equal(15.0m, updated!.CurrentStock);
    }

    [Fact]
    public async Task RestockIngredient_ZeroOrNegativeQuantity_ThrowsException()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var salmon = new Ingredient { Name = "Cá Hồi Nauy", Unit = "kg", CurrentStock = 5.0m, MinStock = 2.0m };
        context.Ingredients.Add(salmon);
        await context.SaveChangesAsync();

        var service = new InventoryService(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RestockIngredientAsync(salmon.IngredientId, 0m));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RestockIngredientAsync(salmon.IngredientId, -3.5m));
    }

    [Fact]
    public async Task GetLowStockAlerts_ReturnsIngredientsAtOrBelowMinStock()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var ing1 = new Ingredient { Name = "Tiêu đen", Unit = "g", CurrentStock = 200m, MinStock = 500m }; // Low
        var ing2 = new Ingredient { Name = "Muối tinh", Unit = "kg", CurrentStock = 10m, MinStock = 2m };   // Safe
        var ing3 = new Ingredient { Name = "Dầu ô liu", Unit = "lit", CurrentStock = 5m, MinStock = 5m };   // At boundary (Low)

        context.Ingredients.AddRange(ing1, ing2, ing3);
        await context.SaveChangesAsync();

        var service = new InventoryService(context);

        // Act
        var alerts = await service.GetLowStockAlertsAsync();

        // Assert
        Assert.Contains(alerts, i => i.Name == "Tiêu đen");
        Assert.Contains(alerts, i => i.Name == "Dầu ô liu");
        Assert.DoesNotContain(alerts, i => i.Name == "Muối tinh");
    }
}
