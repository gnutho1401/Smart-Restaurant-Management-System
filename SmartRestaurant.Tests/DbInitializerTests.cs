using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Data;
using Xunit;

namespace SmartRestaurant.Tests;

/// <summary>
/// Unit tests for DbInitializer verifying initial 3NF catalog seeding and idempotency.
/// </summary>
public class DbInitializerTests
{
    [Fact]
    public async Task InitializeAsync_PopulatesCompleteCatalog_WhenDatabaseEmpty()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();

        // Act
        await DbInitializer.InitializeAsync(context);

        // Assert
        Assert.True(await context.DiningAreas.AnyAsync());
        Assert.True(await context.DiningTables.AnyAsync());
        Assert.True(await context.MenuCategories.AnyAsync());
        Assert.True(await context.Ingredients.AnyAsync());
        Assert.True(await context.MenuItems.AnyAsync());
        Assert.True(await context.Recipes.AnyAsync());
        Assert.True(await context.Users.AnyAsync());

        // Validate table counts
        var tableCount = await context.DiningTables.CountAsync();
        Assert.Equal(10, tableCount);

        // Validate ingredient counts
        var ingredientCount = await context.Ingredients.CountAsync();
        Assert.Equal(11, ingredientCount);

        // Validate user accounts
        var userCount = await context.Users.CountAsync();
        Assert.Equal(4, userCount);
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent_DoesNotDuplicateData()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();

        // First initialization
        await DbInitializer.InitializeAsync(context);
        int initialAreas = await context.DiningAreas.CountAsync();
        int initialDishes = await context.MenuItems.CountAsync();

        // Act - Second initialization should return immediately
        await DbInitializer.InitializeAsync(context);
        int secondAreas = await context.DiningAreas.CountAsync();
        int secondDishes = await context.MenuItems.CountAsync();

        // Assert
        Assert.Equal(initialAreas, secondAreas);
        Assert.Equal(initialDishes, secondDishes);
    }
}

