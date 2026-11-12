using Microsoft.Extensions.DependencyInjection;
using SmartRestaurant.Core.Interfaces;

namespace SmartRestaurant.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IUserSessionService, UserSessionService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<ITableService, TableService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IKdsService, KdsService>();

        return services;
    }
}
