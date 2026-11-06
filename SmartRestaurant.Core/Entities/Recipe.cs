namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Bill-of-Materials (BOM) junction entity specifying raw ingredient consumption per portion.
/// </summary>
public class Recipe
{
    public int RecipeId { get; set; }
    public int MenuItemId { get; set; }
    public int IngredientId { get; set; }
    public decimal QuantityRequired { get; set; }

    public virtual MenuItem? MenuItem { get; set; }
    public virtual Ingredient? Ingredient { get; set; }
}
