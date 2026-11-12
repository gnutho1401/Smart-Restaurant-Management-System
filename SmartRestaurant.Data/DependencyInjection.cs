using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data.Repositories;

namespace SmartRestaurant.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContextFactory<RestaurantDbContext>(configureDbContext);
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<RestaurantDbContext>>().CreateDbContext());

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
