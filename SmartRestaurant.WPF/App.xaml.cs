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

        // 0. Global Unhandled Exception Shielding (Resilience - Prevents silent crashes)
        DispatcherUnhandledException += (s, args) =>
        {
            LogCrash("DispatcherUnhandledException", args.Exception);
            args.Handled = true; // Prevent process exit
            MessageBox.Show(
                $"Đã xảy ra sự cố: {args.Exception.Message}\nHệ thống đã tự phục hồi an toàn.",
                "Thông Báo Hệ Thống",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogCrash("AppDomain.UnhandledException", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogCrash("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved(); // Prevent background task tear-down
        };

        // Explicit shutdown mode ensures smooth transition between MainWindow and LoginWindow upon logout
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

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
            LogCrash("DbInitializer.InitializeAsync", ex);
            MessageBox.Show(
                $"Lưu ý khởi tạo cơ sở dữ liệu: {ex.Message}\nỨng dụng sẽ tiếp tục với cấu hình hiện tại.",
                "Cảnh Báo Khởi Tạo Dữ Liệu",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // Launch Authentication Gateway
        ShowLoginGateway();
    }

    private static void LogCrash(string source, Exception ex)
    {
        try
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            string logFile = Path.Combine(logDir, "crash.log");
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(logFile, entry);
        }
        catch
        {
            // Logging should never throw
        }
    }

    private void ShowLoginGateway()
    {
        var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
        var loginVm = (LoginViewModel)loginWindow.DataContext;

        // Clean application shutdown if user closes login window
        loginWindow.Closed += (s, e) =>
        {
            if (ReferenceEquals(Current.MainWindow, loginWindow))
            {
                Current.Shutdown();
            }
        };

        loginVm.LoginSuccess += (user) =>
        {
            var mainVm = ServiceProvider.GetRequiredService<MainViewModel>();
            mainVm.ApplyInitialRoleView();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();

            // Clean application shutdown if user closes main window directly (X button or Alt+F4)
            mainWindow.Closed += (s, e) =>
            {
                if (ReferenceEquals(Current.MainWindow, mainWindow))
                {
                    Current.Shutdown();
                }
            };

            Action? logoutHandler = null;
            logoutHandler = () =>
            {
                mainVm.RequestLogout -= logoutHandler;
                mainVm.Cleanup();
                ShowLoginGateway();
                mainWindow.Close();
            };
            mainVm.RequestLogout += logoutHandler;

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

        Action<DbContextOptionsBuilder> dbConfig = options =>
        {
            if (string.Equals(dbProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(sqlServerConnection);
            }
            else
            {
                options.UseInMemoryDatabase("SmartRestaurantDb")
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            }
        };

        services.AddDbContextFactory<RestaurantDbContext>(dbConfig);
        services.AddTransient<RestaurantDbContext>(sp => sp.GetRequiredService<IDbContextFactory<RestaurantDbContext>>().CreateDbContext());

        // 2. HTTP Client for LM Studio Local LLM (UseProxy = false to avoid WPAD delay on localhost/127.0.0.1)
        services.AddSingleton<HttpClient>(sp =>
        {
            var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            };
            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(90.0)
            };
        });

        // 3. Domain Business & Security Services
        services.AddSingleton<IAuthService>(sp => new AuthService(sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddTransient<IStaffService, StaffService>();
        services.AddTransient<ITableService, TableService>();
        services.AddTransient<IMenuService, MenuService>();
        services.AddTransient<IOrderService, OrderService>();
        services.AddTransient<IInventoryService, InventoryService>();
        services.AddTransient<IBillingService, BillingService>();
        services.AddTransient<IKdsService, KdsService>();
        services.AddTransient<ICustomerService, CustomerService>();
        services.AddTransient<IShiftService, ShiftService>();
        services.AddTransient<IAuditService, AuditService>();
        services.AddTransient<IAiService, LmStudioService>();
        services.AddTransient<AiAnalyticsEngine>();

        // 4. ViewModels
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
        services.AddTransient<AiChatViewModel>();
        services.AddSingleton<MainViewModel>();

        // 5. Views & Windows
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<AiChatWindow>();
        services.AddTransient<BillingDialog>();
        services.AddTransient<ShiftDialog>();
        services.AddTransient<ReportsView>();
        services.AddTransient<StaffManagementView>();
        services.AddTransient<InventoryManagementView>();
        services.AddTransient<AuditLogsView>();
    }
}
