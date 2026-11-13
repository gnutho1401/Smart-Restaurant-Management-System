using System;
using System.Threading.Tasks;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class CustomerServiceTests
{
    [Fact]
    public async Task RegisterCustomer_ValidPhoneAndName_SucceedsWithInitialTierStandard()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new CustomerService(context);

        var customer = await service.RegisterCustomerAsync("0901234567", "Nguyễn Văn A");

        Assert.NotNull(customer);
        Assert.Equal("0901234567", customer.PhoneNumber);
        Assert.Equal("Nguyễn Văn A", customer.FullName);
        Assert.Equal(0, customer.LoyaltyPoints);
        Assert.Equal(MembershipTier.Standard, customer.Tier);
    }

    [Fact]
    public async Task RegisterCustomer_DuplicatePhoneNumber_ThrowsInvalidOperationException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new CustomerService(context);

        await service.RegisterCustomerAsync("0988888888", "Khách Hàng 1");

        // Enforce uniqueness: duplicate phone number must throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterCustomerAsync("0988888888", "Khách Hàng 2"));
    }

    [Fact]
    public async Task AddLoyaltyPoints_AccruesPointsAndUpgradesTierCorrectly()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new CustomerService(context);

        var customer = await service.RegisterCustomerAsync("0912345678", "Trần Thị B");

        // Spend 1,500,000 VND -> earns 150 points -> upgraded to Silver (>= 100)
        await service.AddLoyaltyPointsAsync(customer.CustomerId, 1500000m);
        var updated = await service.GetCustomerByIdAsync(customer.CustomerId);
        Assert.NotNull(updated);
        Assert.Equal(150, updated.LoyaltyPoints);
        Assert.Equal(MembershipTier.Silver, updated.Tier);

        // Spend another 4,000,000 VND -> earns 400 pts (Total 550) -> upgraded to Gold (>= 500)
        await service.AddLoyaltyPointsAsync(customer.CustomerId, 4000000m);
        updated = await service.GetCustomerByIdAsync(customer.CustomerId);
        Assert.NotNull(updated);
        Assert.Equal(550, updated.LoyaltyPoints);
        Assert.Equal(MembershipTier.Gold, updated.Tier);

        // Spend another 5,000,000 VND -> earns 500 pts (Total 1050) -> upgraded to Diamond (>= 1000)
        await service.AddLoyaltyPointsAsync(customer.CustomerId, 5000000m);
        updated = await service.GetCustomerByIdAsync(customer.CustomerId);
        Assert.NotNull(updated);
        Assert.Equal(1050, updated.LoyaltyPoints);
        Assert.Equal(MembershipTier.Diamond, updated.Tier);
    }

    [Fact]
    public async Task RedeemLoyaltyPoints_ValidAmount_DeductsPoints()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new CustomerService(context);

        var customer = await service.RegisterCustomerAsync("0933333333", "Lê Văn C");
        await service.AddLoyaltyPointsAsync(customer.CustomerId, 2000000m); // 200 points

        bool redeemed = await service.RedeemLoyaltyPointsAsync(customer.CustomerId, 50);
        Assert.True(redeemed);

        var updated = await service.GetCustomerByIdAsync(customer.CustomerId);
        Assert.NotNull(updated);
        Assert.Equal(150, updated.LoyaltyPoints);
    }

    [Fact]
    public async Task RedeemLoyaltyPoints_MoreThanAvailable_ReturnsFalse()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new CustomerService(context);

        var customer = await service.RegisterCustomerAsync("0944444444", "Phạm Văn D");
        await service.AddLoyaltyPointsAsync(customer.CustomerId, 500000m); // 50 points

        bool redeemed = await service.RedeemLoyaltyPointsAsync(customer.CustomerId, 100);
        Assert.False(redeemed);
    }
}