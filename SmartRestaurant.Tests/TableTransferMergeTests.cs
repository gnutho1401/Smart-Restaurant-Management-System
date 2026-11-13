using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class TableTransferMergeTests
{
    [Fact]
    public async Task TransferTable_ActiveOrder_MovesToTargetTableAndUpdatesStatuses()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var tableService = new TableService(context);
        var orderService = new OrderService(context);

        // Seed area, dish and tables
        var area = new DiningArea { AreaName = "Sảnh Chính" };
        var dish = new MenuItem { Name = "Phở Bò", Price = 65000m, IsAvailable = true };
        context.DiningAreas.Add(area);
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var table1 = new DiningTable { TableName = "T-01", Status = TableStatus.Available, Capacity = 4, AreaId = area.AreaId };
        var table2 = new DiningTable { TableName = "T-02", Status = TableStatus.Available, Capacity = 4, AreaId = area.AreaId };
        context.DiningTables.AddRange(table1, table2);
        await context.SaveChangesAsync();

        // Create order on Table 1 (Table 1 becomes Occupied)
        await orderService.CreateOrderAsync(table1.TableId, 1, new List<(int, int, string?)> { (dish.MenuItemId, 2, null) });

        // Transfer from Table 1 to Table 2
        bool transferred = await tableService.TransferTableAsync(table1.TableId, table2.TableId);
        Assert.True(transferred);

        // Table 1 should now be Available, Table 2 should be Occupied
        var t1 = await tableService.GetTableByIdAsync(table1.TableId);
        var t2 = await tableService.GetTableByIdAsync(table2.TableId);
        Assert.NotNull(t1);
        Assert.NotNull(t2);
        Assert.Equal(TableStatus.Available, t1.Status);
        Assert.Equal(TableStatus.Occupied, t2.Status);

        // Order on Table 2 should be active
        var activeOrder = await orderService.GetActiveOrderByTableIdAsync(table2.TableId);
        Assert.NotNull(activeOrder);
        Assert.Equal(table2.TableId, activeOrder.TableId);
    }

    [Fact]
    public async Task MergeTables_TwoActiveOrders_CombinesItemsIntoDestinationOrder()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var tableService = new TableService(context);
        var orderService = new OrderService(context);

        var area = new DiningArea { AreaName = "Sân Vườn" };
        var dish1 = new MenuItem { Name = "Bún Chả", Price = 70000m, IsAvailable = true };
        var dish2 = new MenuItem { Name = "Nem Rán", Price = 50000m, IsAvailable = true };
        context.DiningAreas.Add(area);
        context.MenuItems.AddRange(dish1, dish2);
        await context.SaveChangesAsync();

        var table1 = new DiningTable { TableName = "T-03", Status = TableStatus.Available, Capacity = 4, AreaId = area.AreaId };
        var table2 = new DiningTable { TableName = "T-04", Status = TableStatus.Available, Capacity = 4, AreaId = area.AreaId };
        context.DiningTables.AddRange(table1, table2);
        await context.SaveChangesAsync();

        // Orders on both tables
        await orderService.CreateOrderAsync(table1.TableId, 1, new List<(int, int, string?)> { (dish1.MenuItemId, 1, null) });
        await orderService.CreateOrderAsync(table2.TableId, 1, new List<(int, int, string?)> { (dish2.MenuItemId, 2, null) });

        // Merge Table 2 into Table 1 (Table 1 is primary destination, Table 2 is secondary)
        bool merged = await tableService.MergeTablesAsync(table1.TableId, table2.TableId);
        Assert.True(merged);

        var t1 = await tableService.GetTableByIdAsync(table1.TableId);
        var t2 = await tableService.GetTableByIdAsync(table2.TableId);
        Assert.NotNull(t1);
        Assert.NotNull(t2);
        Assert.Equal(TableStatus.Occupied, t1.Status);
        Assert.Equal(TableStatus.Available, t2.Status);

        // Merged order on primary Table 1 should now contain items from both orders
        var targetOrder = await orderService.GetActiveOrderByTableIdAsync(table1.TableId);
        Assert.NotNull(targetOrder);
        Assert.Equal(2, targetOrder.OrderItems.Count);
        // Total: 70,000 + (2 * 50,000) = 170,000
        Assert.Equal(170000m, targetOrder.SubTotal);
    }
}