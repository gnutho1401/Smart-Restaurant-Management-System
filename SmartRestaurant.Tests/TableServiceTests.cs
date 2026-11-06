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
}
