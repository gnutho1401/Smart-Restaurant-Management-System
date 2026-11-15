using System;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRestaurant.AIEngine;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;
using SmartRestaurant.Services;
using SmartRestaurant.WPF.ViewModels;
using Xunit;

namespace SmartRestaurant.Tests;

/// <summary>
/// Architectural and integration tests verifying the Dependency Injection container integrity,
/// ensuring zero ambiguous constructors and smooth initialization of all 8 POS modules.
/// </summary>
public class DependencyInjectionTests
{
    private static IServiceProvider CreateTestServiceProvider()
    {
        var services = new ServiceCollection();

        // Database context factory and context registration (in-memory for testing)
        services.AddDbContextFactory<RestaurantDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

        services.AddTransient<RestaurantDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<RestaurantDbContext>>().CreateDbContext());

        // HTTP Client
        services.AddSingleton<HttpClient>();

        // Domain Business & Security Services
        services.AddSingleton<IAuthService>(sp => new AuthService(sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddTransient<IStaffService, StaffService>();
        services.AddTransient<ITableService, TableService>();
        services.AddTransient<IMenuService, MenuService>();
        services.AddTransient<IOrderService, OrderService>();
        services.AddTransient<IInventoryService, InventoryService>();
        services.AddTransient<IBillingService, BillingService>();
        services.AddTransient<IKdsService>(sp => new KdsService(sp.GetRequiredService<IDbContextFactory<RestaurantDbContext>>()));
        services.AddTransient<ICustomerService, CustomerService>();
        services.AddTransient<IShiftService, ShiftService>();
        services.AddTransient<IAuditService, AuditService>();
        services.AddTransient<IAiService, LmStudioService>();
        services.AddTransient<AiAnalyticsEngine>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddSingleton<FloorPlanViewModel>();
        services.AddSingleton<PosOrderViewModel>();
        services.AddSingleton<KitchenKdsViewModel>();
        services.AddSingleton<MenuViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<StaffManagementViewModel>();
        services.AddSingleton<InventoryManagementViewModel>();
        services.AddSingleton<AuditLogViewModel>();
        services.AddTransient<BillingViewModel>();
        services.AddTransient<ShiftViewModel>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void DependencyInjection_ResolvesKdsService_WithoutAmbiguousConstructorException()
    {
        var sp = CreateTestServiceProvider();
        var kdsService = sp.GetRequiredService<IKdsService>();

        Assert.NotNull(kdsService);
        Assert.IsType<KdsService>(kdsService);
    }

    [Fact]
    public void DependencyInjection_ResolvesMainViewModel_AndAllChildViewModels_Successfully()
    {
        var sp = CreateTestServiceProvider();

        var mainVm = sp.GetRequiredService<MainViewModel>();
        Assert.NotNull(mainVm);
        Assert.NotNull(mainVm.FloorPlanVm);
        Assert.NotNull(mainVm.PosOrderVm);
        Assert.NotNull(mainVm.KitchenKdsVm);
        Assert.NotNull(mainVm.MenuVm);
        Assert.NotNull(mainVm.ReportsVm);
        Assert.NotNull(mainVm.StaffVm);
        Assert.NotNull(mainVm.InventoryVm);
        Assert.NotNull(mainVm.AuditLogVm);
    }

    [Theory]
    [InlineData("admin", "admin123", UserRole.Admin, "Reports")]
    [InlineData("cashier", "cashier123", UserRole.Cashier, "FloorPlan")]
    [InlineData("manager", "manager123", UserRole.StoreManager, "FloorPlan")]
    [InlineData("chef", "chef123", UserRole.HeadChef, "KDS")]
    public async System.Threading.Tasks.Task LoginFlow_ResolvesAndDirectsUser_ToCorrectOperationalScreen(
        string username, string password, UserRole expectedRole, string expectedViewKey)
    {
        var sp = CreateTestServiceProvider();
        var authService = sp.GetRequiredService<IAuthService>();
        var loginVm = sp.GetRequiredService<LoginViewModel>();

        // Act - Authenticate
        var user = await authService.LoginAsync(username, password);
        Assert.NotNull(user);
        Assert.Equal(expectedRole, user.Role);

        // Act - Simulate LoginSuccess event in App.xaml.cs
        var mainVm = sp.GetRequiredService<MainViewModel>();
        mainVm.IsScreenLocked = false;
        mainVm.ApplyInitialRoleView();

        // Assert - Screen navigation and role permissions
        Assert.False(mainVm.IsScreenLocked);
        Assert.NotNull(mainVm.CurrentView);

        if (expectedViewKey == "Reports")
        {
            Assert.Same(mainVm.ReportsVm, mainVm.CurrentView);
            Assert.True(mainVm.IsReportsAllowed);
        }
        else if (expectedViewKey == "KDS")
        {
            Assert.Same(mainVm.KitchenKdsVm, mainVm.CurrentView);
            Assert.True(mainVm.IsKdsAllowed);
        }
        else
        {
            Assert.Same(mainVm.FloorPlanVm, mainVm.CurrentView);
            Assert.True(mainVm.IsFloorPlanAllowed);
        }
    }
}

