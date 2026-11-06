using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Raw inventory ingredient entity with optimistic concurrency token for atomic BOM consumption.
/// </summary>
public class Ingredient
{
    public int IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "g";

    [ConcurrencyCheck]
    public decimal CurrentStock { get; set; }

    public decimal MinStock { get; set; } = 0;

    public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
}
