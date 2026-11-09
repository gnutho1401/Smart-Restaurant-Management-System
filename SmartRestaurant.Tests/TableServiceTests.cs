using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Exceptions;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class TableServiceTests
{
    [Fact]
    public async Task Transition_FromAvailableToOccupied_Succeeds()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-01", Status = TableStatus.Available, Capacity = 4 };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var service = new TableService(context);
        bool result = await service.TransitionTableStatusAsync(table.TableId, TableStatus.Occupied);

        Assert.True(result);
        Assert.Equal(TableStatus.Occupied, table.Status);
    }

    [Fact]
    public async Task Transition_FromAvailableToReserved_Succeeds()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "VIP-01", Status = TableStatus.Available, Capacity = 6 };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var service = new TableService(context);
        bool result = await service.TransitionTableStatusAsync(table.TableId, TableStatus.Reserved);

        Assert.True(result);
        Assert.Equal(TableStatus.Reserved, table.Status);
    }

    [Fact]
    public async Task Transition_FromReservedToOccupied_Succeeds()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-02", Status = TableStatus.Reserved, Capacity = 2 };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var service = new TableService(context);
        bool result = await service.TransitionTableStatusAsync(table.TableId, TableStatus.Occupied);

        Assert.True(result);
        Assert.Equal(TableStatus.Occupied, table.Status);
    }

    [Fact]
    public async Task Transition_FromReservedToAvailable_CancelBooking_Succeeds()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-03", Status = TableStatus.Reserved, Capacity = 4 };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var service = new TableService(context);
        bool result = await service.TransitionTableStatusAsync(table.TableId, TableStatus.Available);

        Assert.True(result);
        Assert.Equal(TableStatus.Available, table.Status);
    }

    [Fact]
    public async Task Transition_FromOccupiedToCleaning_Succeeds()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-04", Status = TableStatus.Occupied, Capacity = 4 };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var service = new TableService(context);
        bool result = await service.TransitionTableStatusAsync(table.TableId, TableStatus.Cleaning);

        Assert.True(result);
        Assert.Equal(TableStatus.Cleaning, table.Status);
    }

    [Fact]
    public async Task Transition_FromCleaningToAvailable_TableSanitized_Succeeds()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-04B", Status = TableStatus.Cleaning, Capacity = 4 };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var service = new TableService(context);
        bool result = await service.TransitionTableStatusAsync(table.TableId, TableStatus.Available);

        Assert.True(result);
        Assert.Equal(TableStatus.Available, table.Status);
    }

    [Fact]
    public async Task Transition_SameStatus_ReturnsTrue_NoOp()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-04C", Status = TableStatus.Available, Capacity = 4 };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var service = new TableService(context);
        bool result = await service.TransitionTableStatusAsync(table.TableId, TableStatus.Available);

        Assert.True(result);
    }

    [Fact]
    public async Task Transition_NonExistentTable_ThrowsKeyNotFoundException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new TableService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.TransitionTableStatusAsync(9999, TableStatus.Occupied));
    }

    [Fact]
    public async Task Transition_InvalidDirectOccupiedToAvailable_ThrowsInvalidStatusTransitionException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "T-05", Status = TableStatus.Occupied, Capacity = 4 };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var service = new TableService(context);

        // Invariant: Direct Occupied -> Available is forbidden without passing through Cleaning
        await Assert.ThrowsAsync<InvalidStatusTransitionException>(() =>
            service.TransitionTableStatusAsync(table.TableId, TableStatus.Available));
    }

    [Fact]
    public async Task GetAllTablesAndGetById_ReturnExpectedEntities()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var area = new DiningArea { AreaName = "Sảnh", FloorNumber = 1 };
        context.DiningAreas.Add(area);
        await context.SaveChangesAsync();

        var table1 = new DiningTable { TableName = "T-10", Capacity = 4, AreaId = area.AreaId };
        var table2 = new DiningTable { TableName = "T-11", Capacity = 2, AreaId = area.AreaId };
        context.DiningTables.AddRange(table1, table2);
        await context.SaveChangesAsync();

        var service = new TableService(context);

        var all = (await service.GetAllTablesAsync()).ToList();
        var single = await service.GetTableByIdAsync(table1.TableId);

        Assert.Equal(2, all.Count);
        Assert.NotNull(single);
        Assert.Equal("T-10", single.TableName);
    }
}
