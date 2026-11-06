using System;
using System.Net.Http;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRestaurant.AIEngine;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;
using SmartRestaurant.Services;

namespace SmartRestaurant.WPF;

/// <summary>
/// Application startup bootstrapper configuring the Dependency Injection container
/// and idempotent database seeding.
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

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
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

        // 3. Domain Business Services
        services.AddScoped<ITableService, TableService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IKdsService, KdsService>();
        services.AddScoped<IAiService, LmStudioService>();
        services.AddScoped<AiAnalyticsEngine>();

        // 4. Windows
        services.AddTransient<MainWindow>();
    }
}
