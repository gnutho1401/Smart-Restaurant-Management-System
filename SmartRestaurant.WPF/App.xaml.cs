using System;
using System.Net.Http;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRestaurant.AIEngine;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;
using SmartRestaurant.Services;
using SmartRestaurant.WPF.ViewModels;
using SmartRestaurant.WPF.Views;

namespace SmartRestaurant.WPF;

/// <summary>
/// Application startup bootstrapper configuring the Dependency Injection container,
/// idempotent database seeding, and secure authentication gateway.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);

        ServiceProvider = services.BuildServiceProvider();

        // Initialize and seed database asynchronously
        using (var scope = ServiceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();
            await DbInitializer.InitializeAsync(dbContext);
        }

        // Launch Authentication Gateway
        var loginVm = ServiceProvider.GetRequiredService<LoginViewModel>();
        var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();

        loginVm.LoginSuccess += (user) =>
        {
            var mainVm = ServiceProvider.GetRequiredService<MainViewModel>();
            mainVm.ApplyInitialRoleView();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            Current.MainWindow = mainWindow;
            mainWindow.Show();
            loginWindow.Close();
        };

        Current.MainWindow = loginWindow;
        loginWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 1. Database Context (Supports In-Memory for testability & LocalDB)
        services.AddDbContext<RestaurantDbContext>(options =>
        {
            options.UseInMemoryDatabase("SmartRestaurantDb");
        });

        // 2. HTTP Client for LM Studio Local LLM
        services.AddSingleton<HttpClient>();

        // 3. Domain Business & Security Services
        services.AddSingleton<IAuthService, AuthService>();
        services.AddScoped<ITableService, TableService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IKdsService, KdsService>();
        services.AddScoped<IAiService, LmStudioService>();
        services.AddScoped<AiAnalyticsEngine>();

        // 4. ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddSingleton<FloorPlanViewModel>();
        services.AddSingleton<PosOrderViewModel>();
        services.AddSingleton<KitchenKdsViewModel>();
        services.AddSingleton<MenuViewModel>();
        services.AddTransient<BillingViewModel>();
        services.AddTransient<AiChatViewModel>();
        services.AddSingleton<MainViewModel>();

        // 5. Views & Windows
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<AiChatWindow>();
        services.AddTransient<BillingDialog>();
    }
}
