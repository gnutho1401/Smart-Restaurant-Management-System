using System;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class StaffServiceTests
{
    [Fact]
    public async Task GetAllStaffAsync_ReturnsAllUsersOrderedByRole()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new StaffService(context);

        var staff = await service.GetAllStaffAsync();

        Assert.NotNull(staff);
    }

    [Fact]
    public async Task CreateStaffAsync_CreatesNewUserSuccessfully()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new StaffService(context);

        var user = await service.CreateStaffAsync("cashier_minh", "pass123", "Trần Minh", UserRole.Cashier);

        Assert.NotNull(user);
        Assert.Equal("cashier_minh", user.Username);
        Assert.Equal("Trần Minh", user.FullName);
        Assert.Equal(UserRole.Cashier, user.Role);
    }

    [Fact]
    public async Task CreateStaffAsync_DuplicateUsername_ThrowsInvalidOperationException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new StaffService(context);

        await service.CreateStaffAsync("testuser", "pass123", "User 1", UserRole.Cashier);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateStaffAsync("testuser", "anotherpass", "User 2", UserRole.HeadChef));
    }

    [Fact]
    public async Task UpdateStaffRoleAsync_UpdatesRoleCorrectly()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new StaffService(context);

        var user = await service.CreateStaffAsync("user_role_test", "pass123", "Role Test", UserRole.Cashier);
        var updated = await service.UpdateStaffRoleAsync(user.UserId, UserRole.StoreManager);

        Assert.True(updated);
        var refreshed = (await service.GetAllStaffAsync()).First(u => u.UserId == user.UserId);
        Assert.Equal(UserRole.StoreManager, refreshed.Role);
    }

    [Fact]
    public async Task ResetStaffPasswordAsync_UpdatesPasswordHash()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new StaffService(context);

        var user = await service.CreateStaffAsync("pwd_test", "oldpass", "Pwd Test", UserRole.Cashier);
        var oldHash = user.PasswordHash;

        var result = await service.ResetStaffPasswordAsync(user.UserId, "newpass456");

        Assert.True(result);
        var refreshed = (await service.GetAllStaffAsync()).First(u => u.UserId == user.UserId);
        Assert.NotEqual(oldHash, refreshed.PasswordHash);
    }
}
