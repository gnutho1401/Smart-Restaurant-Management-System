using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Service contract for menu catalog and BOM recipe formulation.
/// </summary>
public interface IMenuService
{
    Task<IEnumerable<MenuItem>> GetAllMenuItemsAsync();
    Task<IEnumerable<MenuItem>> GetAvailableMenuItemsAsync();
    Task<IEnumerable<MenuCategory>> GetAllCategoriesAsync();
    Task<MenuItem?> GetMenuItemWithRecipeAsync(int menuItemId);
    Task<bool> UpsertMenuItemAsync(MenuItem item);
    Task<bool> DeleteMenuItemAsync(int menuItemId);
}
