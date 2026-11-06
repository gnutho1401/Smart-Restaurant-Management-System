using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartRestaurant.Data;

namespace SmartRestaurant.Tests;

public static class TestDbContextFactory
{
    public static RestaurantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new RestaurantDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
