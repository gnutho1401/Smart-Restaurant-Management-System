using System;
using System.Threading.Tasks;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class ShiftServiceTests
{
    [Fact]
    public async Task OpenShift_ValidOpeningFloat_CreatesActiveShift()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new ShiftService(context);

        var shift = await service.OpenShiftAsync(userId: 1, openingFloat: 1000000m);

        Assert.NotNull(shift);
        Assert.Equal(1, shift.UserId);
        Assert.Equal(1000000m, shift.OpeningFloat);
        Assert.False(shift.IsClosed);
    }

    [Fact]
    public async Task OpenShift_AlreadyHasOpenShift_ThrowsInvalidOperationException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new ShiftService(context);

        await service.OpenShiftAsync(userId: 2, openingFloat: 500000m);

        // Cannot open another shift when one is already active
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.OpenShiftAsync(userId: 2, openingFloat: 500000m));
    }

    [Fact]
    public async Task CloseShift_BalancedCash_ComputesZeroVariance()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new ShiftService(context);

        var shift = await service.OpenShiftAsync(userId: 3, openingFloat: 1000000m);
        // Expected cash end = 1,000,000 (Float) + 0 (Sales) = 1,000,000
        var closedShift = await service.CloseShiftAsync(shift.ShiftId, actualCashEnd: 1000000m, "Khớp tiền");

        Assert.True(closedShift.IsClosed);
        Assert.Equal(1000000m, closedShift.ActualCashEnd);
        Assert.Equal(0m, closedShift.CashVariance);
    }

    [Fact]
    public async Task CloseShift_CashShortage_ComputesNegativeVariance()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new ShiftService(context);

        var shift = await service.OpenShiftAsync(userId: 4, openingFloat: 1000000m);
        // Cashier reports 950,000 VND -> Shortage -50,000 VND
        var closedShift = await service.CloseShiftAsync(shift.ShiftId, actualCashEnd: 950000m, "Thiếu 50k");

        Assert.True(closedShift.IsClosed);
        Assert.Equal(-50000m, closedShift.CashVariance);
    }

    [Fact]
    public async Task CloseShift_CashSurplus_ComputesPositiveVariance()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new ShiftService(context);

        var shift = await service.OpenShiftAsync(userId: 5, openingFloat: 1000000m);
        // Cashier reports 1,030,000 VND -> Surplus +30,000 VND
        var closedShift = await service.CloseShiftAsync(shift.ShiftId, actualCashEnd: 1030000m, "Dư 30k");

        Assert.True(closedShift.IsClosed);
        Assert.Equal(30000m, closedShift.CashVariance);
    }
}