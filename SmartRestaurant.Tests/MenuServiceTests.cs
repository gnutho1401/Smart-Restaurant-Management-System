using System;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class MenuServiceTests
{
    [Fact]
    public async Task AddDish_WithValidPrice_InsertsRecord()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var cat = new MenuCategory { CategoryName = "Món Chính" };
        context.MenuCategories.Add(cat);
        await context.SaveChangesAsync();

        var service = new MenuService(context);
        var dish = new MenuItem
        {
            Name = "Bò Wagyu Thượng Hạng",
            Price = 450000m,
            CategoryId = cat.CategoryId,
            IsAvailable = true
        };

        bool result = await service.UpsertMenuItemAsync(dish);
        Assert.True(result);

        var saved = await service.GetMenuItemWithRecipeAsync(dish.MenuItemId);
        Assert.NotNull(saved);
        Assert.Equal("Bò Wagyu Thượng Hạng", saved.Name);
        Assert.Equal(450000m, saved.Price);
    }

    [Fact]
    public async Task AddDish_WithNegativePrice_ThrowsException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new MenuService(context);
        var dish = new MenuItem { Name = "Món Lỗi", Price = -50000m, CategoryId = 1 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.UpsertMenuItemAsync(dish));
    }

    [Fact]
    public async Task AddDish_WithEmptyName_ThrowsException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new MenuService(context);
        var dish = new MenuItem { Name = "   ", Price = 50000m, CategoryId = 1 };

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpsertMenuItemAsync(dish));
    }

    [Fact]
    public async Task GetAvailableMenuItems_ReturnsOnlyAvailableDishes()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var cat = new MenuCategory { CategoryName = "Món Chính" };
        context.MenuCategories.Add(cat);
        await context.SaveChangesAsync();

        context.MenuItems.AddRange(
            new MenuItem { Name = "Món 1", Price = 100000m, IsAvailable = true, CategoryId = cat.CategoryId },
            new MenuItem { Name = "Món 2", Price = 150000m, IsAvailable = false, CategoryId = cat.CategoryId },
            new MenuItem { Name = "Món 3", Price = 200000m, IsAvailable = true, CategoryId = cat.CategoryId }
        );
        await context.SaveChangesAsync();

        var service = new MenuService(context);
        var available = await service.GetAvailableMenuItemsAsync();

        Assert.Equal(2, available.Count());
        Assert.DoesNotContain(available, m => !m.IsAvailable);
    }

    [Fact]
    public async Task DeleteMenuItem_PerformsSoftDelete()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var cat = new MenuCategory { CategoryName = "Khai Vị" };
        context.MenuCategories.Add(cat);
        await context.SaveChangesAsync();

        var dish = new MenuItem { Name = "Món Cần Xóa", Price = 80000m, IsAvailable = true, CategoryId = cat.CategoryId };
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var service = new MenuService(context);
        bool deleted = await service.DeleteMenuItemAsync(dish.MenuItemId);

        Assert.True(deleted);
        var itemInDb = await service.GetMenuItemWithRecipeAsync(dish.MenuItemId);
        Assert.NotNull(itemInDb);
        Assert.False(itemInDb.IsAvailable); // Soft delete verified
    }
}
