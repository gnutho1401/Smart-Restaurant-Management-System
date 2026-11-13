using System;
using System.Threading.Tasks;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Security;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class RoleHierarchyTests
{
    [Fact]
    public void RoleHierarchy_EnforcesRanksStrictly()
    {
        Assert.True(RoleHierarchy.GetRank(UserRole.Admin) > RoleHierarchy.GetRank(UserRole.StoreManager));
        Assert.True(RoleHierarchy.GetRank(UserRole.StoreManager) > RoleHierarchy.GetRank(UserRole.HeadChef));
        Assert.True(RoleHierarchy.GetRank(UserRole.HeadChef) > RoleHierarchy.GetRank(UserRole.Cashier));
    }

    [Fact]
    public void Subordinates_CannotManageSuperiorsOrPeers()
    {
        // Cashier cannot manage anyone above or peer
        Assert.False(RoleHierarchy.CanManage(UserRole.Cashier, UserRole.Cashier));
        Assert.False(RoleHierarchy.CanManage(UserRole.Cashier, UserRole.HeadChef));
        Assert.False(RoleHierarchy.CanManage(UserRole.Cashier, UserRole.StoreManager));
        Assert.False(RoleHierarchy.CanManage(UserRole.Cashier, UserRole.Admin));

        // HeadChef cannot manage StoreManager or Admin
        Assert.False(RoleHierarchy.CanManage(UserRole.HeadChef, UserRole.HeadChef));
        Assert.False(RoleHierarchy.CanManage(UserRole.HeadChef, UserRole.StoreManager));
        Assert.False(RoleHierarchy.CanManage(UserRole.HeadChef, UserRole.Admin));

        // StoreManager cannot manage Admin or peer StoreManager
        Assert.False(RoleHierarchy.CanManage(UserRole.StoreManager, UserRole.StoreManager));
        Assert.False(RoleHierarchy.CanManage(UserRole.StoreManager, UserRole.Admin));

        // Admin can manage all lower roles
        Assert.True(RoleHierarchy.CanManage(UserRole.Admin, UserRole.StoreManager));
        Assert.True(RoleHierarchy.CanManage(UserRole.Admin, UserRole.HeadChef));
        Assert.True(RoleHierarchy.CanManage(UserRole.Admin, UserRole.Cashier));
    }

    [Fact]
    public async Task DeleteStaffAsync_AttemptingToDeleteAdmin_ThrowsInvalidOperationException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new StaffService(context);

        var adminUser = await service.CreateStaffAsync("test_admin_del", "pass", "Test Admin", UserRole.Admin);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteStaffAsync(adminUser.UserId));
    }

    [Fact]
    public async Task UpdateStaffRoleAsync_AttemptingToDemoteAdmin_ThrowsInvalidOperationException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new StaffService(context);

        var adminUser = await service.CreateStaffAsync("test_admin_demote", "pass", "Test Admin Demote", UserRole.Admin);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateStaffRoleAsync(adminUser.UserId, UserRole.Cashier));
    }
}

