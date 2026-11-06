using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class OrderServiceTests
{
    [Fact]
    public async Task CalculateSubTotal_MultipleDishes_ReturnsSum()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-01", Status = TableStatus.Available, Capacity = 4 };
        var dish1 = new MenuItem { Name = "Món A", Price = 100000m, CategoryId = 1 };
        var dish2 = new MenuItem { Name = "Món B", Price = 250000m, CategoryId = 1 };

        context.DiningTables.Add(table);
        context.MenuItems.AddRange(dish1, dish2);
        await context.SaveChangesAsync();

        var service = new OrderService(context);
        var items = new List<(int menuItemId, int quantity, string? note)>
        {
            (dish1.MenuItemId, 2, null), // 200,000
            (dish2.MenuItemId, 3, null)  // 750,000
        };

        var order = await service.CreateOrderAsync(table.TableId, 1, items);

        Assert.NotNull(order);
        Assert.Equal(950000m, order.SubTotal);
        Assert.Equal(950000m, order.FinalTotal);
    }

    [Fact]
    public async Task AttachAllergyNote_StoresStringCorrectly()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-02", Status = TableStatus.Available, Capacity = 2 };
        var dish = new MenuItem { Name = "Súp Hải Sản", Price = 120000m, CategoryId = 1 };

        context.DiningTables.Add(table);
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var service = new OrderService(context);
        string warning = "KHÁCH DỊ ỨNG ĐẬU PHỘNG, BẾP LƯU Ý!";
        var items = new List<(int menuItemId, int quantity, string? note)>
        {
            (dish.MenuItemId, 1, warning)
        };

        var order = await service.CreateOrderAsync(table.TableId, 1, items);
        var item = order.OrderItems.First();

        Assert.Equal(warning, item.Note);
    }

    [Fact]
    public async Task CreateOrder_WithEmptyItems_ThrowsArgumentException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new OrderService(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrderAsync(1, 1, new List<(int, int, string?)>()));
    }

    [Fact]
    public async Task CreateOrder_WithNegativeQuantity_ThrowsException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-03", Status = TableStatus.Available, Capacity = 4 };
        var dish = new MenuItem { Name = "Món C", Price = 50000m, CategoryId = 1 };
        context.DiningTables.Add(table);
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var service = new OrderService(context);
        var items = new List<(int menuItemId, int quantity, string? note)>
        {
            (dish.MenuItemId, -2, null)
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateOrderAsync(table.TableId, 1, items));
    }

    [Fact]
    public async Task CreateOrder_OnAvailableTable_TransitionsTableToOccupied()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-04", Status = TableStatus.Available, Capacity = 4 };
        var dish = new MenuItem { Name = "Món D", Price = 90000m, CategoryId = 1 };
        context.DiningTables.Add(table);
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var service = new OrderService(context);
        var items = new List<(int menuItemId, int quantity, string? note)> { (dish.MenuItemId, 1, null) };

        await service.CreateOrderAsync(table.TableId, 1, items);

        Assert.Equal(TableStatus.Occupied, table.Status);
    }

    [Fact]
    public async Task GetActiveOrderByTableId_ReturnsActiveOrder()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-05", Status = TableStatus.Occupied, Capacity = 4 };
        var dish = new MenuItem { Name = "Món E", Price = 80000m, CategoryId = 1 };
        context.DiningTables.Add(table);
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var service = new OrderService(context);
        var items = new List<(int menuItemId, int quantity, string? note)> { (dish.MenuItemId, 2, null) };
        var created = await service.CreateOrderAsync(table.TableId, 1, items);

        var retrieved = await service.GetActiveOrderByTableIdAsync(table.TableId);

        Assert.NotNull(retrieved);
        Assert.Equal(created.OrderId, retrieved.OrderId);
        Assert.Equal("Active", retrieved.Status);
    }

    [Fact]
    public async Task UpdateCookingStatus_TransitionsStatus()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-06", Status = TableStatus.Available, Capacity = 2 };
        var dish = new MenuItem { Name = "Món F", Price = 60000m, CategoryId = 1 };
        context.DiningTables.Add(table);
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var service = new OrderService(context);
        var items = new List<(int menuItemId, int quantity, string? note)> { (dish.MenuItemId, 1, null) };
        var order = await service.CreateOrderAsync(table.TableId, 1, items);
        var orderItem = order.OrderItems.First();

        bool updated = await service.UpdateItemCookingStatusAsync(orderItem.OrderItemId, CookingStatus.Cooking);

        Assert.True(updated);
        Assert.Equal(CookingStatus.Cooking, orderItem.CookingStatus);
    }
}
