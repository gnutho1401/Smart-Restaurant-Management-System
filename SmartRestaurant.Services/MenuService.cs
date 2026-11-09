using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;

namespace SmartRestaurant.Services;

/// <summary>
/// Service managing restaurant menu catalog, categories, and Bill-of-Materials (BOM) formulation.
/// </summary>
public class MenuService : IMenuService
{
    private readonly RestaurantDbContext _context;

    public MenuService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<MenuItem>> GetAllMenuItemsAsync()
    {
        return await _context.MenuItems
            .Include(m => m.Category)
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredient)
            .OrderBy(m => m.CategoryId)
            .ThenBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<MenuItem>> GetAvailableMenuItemsAsync()
    {
        return await _context.MenuItems
            .Include(m => m.Category)
            .Where(m => m.IsAvailable)
            .OrderBy(m => m.CategoryId)
            .ThenBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<MenuCategory>> GetAllCategoriesAsync()
    {
        return await _context.MenuCategories
            .Include(c => c.MenuItems)
            .OrderBy(c => c.CategoryId)
            .ToListAsync();
    }

    public async Task<MenuItem?> GetMenuItemWithRecipeAsync(int menuItemId)
    {
        return await _context.MenuItems
            .Include(m => m.Category)
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredient)
            .FirstOrDefaultAsync(m => m.MenuItemId == menuItemId);
    }

    public async Task<bool> UpsertMenuItemAsync(MenuItem item)
    {
        if (item.Price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(item.Price), "Giá món ăn không được là số âm.");
        }

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            throw new ArgumentException("Tên món ăn không được để trống.", nameof(item.Name));
        }

        if (item.MenuItemId == 0)
        {
            await _context.MenuItems.AddAsync(item);
        }
        else
        {
            var existing = await _context.MenuItems
                .Include(m => m.Recipes)
                .FirstOrDefaultAsync(m => m.MenuItemId == item.MenuItemId);

            if (existing == null)
            {
                throw new KeyNotFoundException($"Menu item ID {item.MenuItemId} not found.");
            }

            existing.Name = item.Name;
            existing.Price = item.Price;
            existing.CategoryId = item.CategoryId;
            existing.Description = item.Description;
            existing.ImageUrl = item.ImageUrl;
            existing.IsAvailable = item.IsAvailable;

            // Synchronize BOM Recipes
            if (item.Recipes.Any())
            {
                var newRecipes = item.Recipes.ToList();
                var dbRecipes = existing.Recipes.Where(r => r.RecipeId > 0).ToList();
                if (dbRecipes.Any())
                {
                    _context.Recipes.RemoveRange(dbRecipes);
                }
                existing.Recipes.Clear();
                foreach (var recipe in newRecipes)
                {
                    existing.Recipes.Add(new Recipe
                    {
                        MenuItemId = existing.MenuItemId,
                        IngredientId = recipe.IngredientId,
                        QuantityRequired = recipe.QuantityRequired
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteMenuItemAsync(int menuItemId)
    {
        var item = await _context.MenuItems.FirstOrDefaultAsync(m => m.MenuItemId == menuItemId);
        if (item == null) return false;

        // Soft delete to protect financial/order historical integrity
        item.IsAvailable = false;
        await _context.SaveChangesAsync();
        return true;
    }
}
