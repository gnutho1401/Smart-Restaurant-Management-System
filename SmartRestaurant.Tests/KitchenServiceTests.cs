using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

/// <summary>
/// Unit tests for Kitchen Display System (KDS) service and ticket queue lifecycle.
/// Validates ticket retrieval, cooking status transitions, and error handling.
/// </summary>
public class KitchenServiceTests
{
    [Fact]
    public async Task GetActiveKitchenTicketsAsync_ReturnsOnlyActiveOrderTicketsNotServed()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-01", Capacity = 4, Status = TableStatus.Occupied };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var activeOrder = new Order { TableId = table.TableId, Status = "Active" };
        var completedOrder = new Order { TableId = table.TableId, Status = "Completed" };
        context.Orders.AddRange(activeOrder, completedOrder);
        await context.SaveChangesAsync();

        var dish1 = new MenuItem { Name = "Phở Bò", Price = 60000m };
        var dish2 = new MenuItem { Name = "Bún Chả", Price = 70000m };
        context.MenuItems.AddRange(dish1, dish2);
        await context.SaveChangesAsync();

        // Ticket 1: Active order, Pending (Should be returned)
        var item1 = new OrderItem { OrderId = activeOrder.OrderId, MenuItemId = dish1.MenuItemId, Quantity = 1, CookingStatus = CookingStatus.Pending };
        // Ticket 2: Active order, Cooking (Should be returned)
        var item2 = new OrderItem { OrderId = activeOrder.OrderId, MenuItemId = dish2.MenuItemId, Quantity = 2, CookingStatus = CookingStatus.Cooking };
        // Ticket 3: Active order, Served (Should NOT be returned)
        var item3 = new OrderItem { OrderId = activeOrder.OrderId, MenuItemId = dish1.MenuItemId, Quantity = 1, CookingStatus = CookingStatus.Served };
        // Ticket 4: Completed order, Cooking (Should NOT be returned because order is not Active)
        var item4 = new OrderItem { OrderId = completedOrder.OrderId, MenuItemId = dish2.MenuItemId, Quantity = 1, CookingStatus = CookingStatus.Cooking };

        context.OrderItems.AddRange(item1, item2, item3, item4);
        await context.SaveChangesAsync();

        var service = new KdsService(context);

        // Act
        var tickets = (await service.GetActiveKitchenTicketsAsync()).ToList();

        // Assert
        Assert.Equal(2, tickets.Count);
        Assert.Contains(tickets, t => t.OrderItemId == item1.OrderItemId);
        Assert.Contains(tickets, t => t.OrderItemId == item2.OrderItemId);
        Assert.DoesNotContain(tickets, t => t.OrderItemId == item3.OrderItemId);
        Assert.DoesNotContain(tickets, t => t.OrderItemId == item4.OrderItemId);
    }

    [Fact]
    public async Task UpdateTicketStatusAsync_TransitionsStatusCorrectly()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var item = new OrderItem { OrderId = 1, MenuItemId = 1, Quantity = 1, CookingStatus = CookingStatus.Pending };
        context.OrderItems.Add(item);
        await context.SaveChangesAsync();

        var service = new KdsService(context);

        // Act - Transition to Cooking
        bool cookingSuccess = await service.UpdateTicketStatusAsync(item.OrderItemId, CookingStatus.Cooking);
        var updatedCooking = await context.OrderItems.FindAsync(item.OrderItemId);
        Assert.True(cookingSuccess);
        Assert.Equal(CookingStatus.Cooking, updatedCooking!.CookingStatus);

        // Act - Transition to Ready
        bool readySuccess = await service.UpdateTicketStatusAsync(item.OrderItemId, CookingStatus.Ready);
        var updatedReady = await context.OrderItems.FindAsync(item.OrderItemId);
        Assert.True(readySuccess);
        Assert.Equal(CookingStatus.Ready, updatedReady!.CookingStatus);
    }

    [Fact]
    public async Task UpdateTicketStatusAsync_NonExistentItem_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new KdsService(context);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateTicketStatusAsync(9999, CookingStatus.Cooking));
    }

    [Fact]
    public async Task GetActiveKitchenTicketsAsync_EmptyQueue_ReturnsEmptyCollection()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new KdsService(context);

        // Act
        var tickets = await service.GetActiveKitchenTicketsAsync();

        // Assert
        Assert.Empty(tickets);
    }

    [Fact]
    public async Task GetActiveKitchenTicketsAsync_LoadsCustomerAndTableNavigationsSafely()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var customer = new Customer { PhoneNumber = "0987654321", FullName = "Nguyễn Văn VIP" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var table = new DiningTable { TableName = "VIP-01", Capacity = 6, Status = TableStatus.Occupied };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var activeOrder = new Order
        {
            TableId = table.TableId,
            CustomerId = customer.CustomerId,
            Status = "Active"
        };
        context.Orders.Add(activeOrder);
        await context.SaveChangesAsync();

        var dish = new MenuItem { Name = "Bò Wagyu A5", Price = 450000m };
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var item = new OrderItem
        {
            OrderId = activeOrder.OrderId,
            MenuItemId = dish.MenuItemId,
            Quantity = 2,
            CookingStatus = CookingStatus.Pending
        };
        context.OrderItems.Add(item);
        await context.SaveChangesAsync();

        var service = new KdsService(context);

        // Act
        var tickets = (await service.GetActiveKitchenTicketsAsync()).ToList();

        // Assert
        Assert.Single(tickets);
        var ticket = tickets[0];
        Assert.Equal("VIP-01", ticket.Order?.Table?.TableName);
        Assert.Equal(customer.CustomerId, ticket.Order?.CustomerId);
    }
}
