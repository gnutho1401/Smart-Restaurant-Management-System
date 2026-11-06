using System.Collections.Generic;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Menu catalog item entity with price, description, and BOM formulation linkage.
/// </summary>
public class MenuItem
{
    public int MenuItemId { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;

    public virtual MenuCategory? Category { get; set; }
    public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
}
