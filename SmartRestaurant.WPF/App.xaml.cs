using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        try
        {
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();
            await DbInitializer.InitializeAsync(dbContext);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Lưu ý khởi tạo cơ sở dữ liệu: {ex.Message}\nỨng dụng sẽ tiếp tục với cấu hình hiện tại.",
                "Cảnh Báo Khởi Tạo Dữ Liệu",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
        // 1. Database Context (Supports In-Memory for testability & SQL Server via appsettings.json)
        string dbProvider = "InMemory";
        string sqlServerConnection = "Server=(localdb)\\mssqllocaldb;Database=SmartRestaurantDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (File.Exists(configPath))
        {
            try
            {
                string json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("DatabaseProvider", out var providerElem))
                {
                    dbProvider = providerElem.GetString() ?? "InMemory";
                }
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var connElem) &&
                    connElem.TryGetProperty("DefaultConnection", out var defaultConnElem))
                {
                    sqlServerConnection = defaultConnElem.GetString() ?? sqlServerConnection;
                }
            }
            catch
            {
                // Fallback to default in memory on read error
            }
        }

        services.AddDbContext<RestaurantDbContext>(options =>
        {
            if (string.Equals(dbProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(sqlServerConnection, b =>
                    b.EnableRetryOnFailure(3, TimeSpan.FromSeconds(3), null));
            }
            else
            {
                options.UseInMemoryDatabase("SmartRestaurantDb")
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            }
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
