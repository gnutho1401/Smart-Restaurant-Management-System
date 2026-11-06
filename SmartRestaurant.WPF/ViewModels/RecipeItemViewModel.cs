using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// Presentation model for raw ingredient recipe line item in BOM editor.
/// </summary>
public class RecipeItemViewModel : ViewModelBase
{
    private decimal _quantityRequired;

    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string Unit { get; set; } = "g";

    public decimal QuantityRequired
    {
        get => _quantityRequired;
        set => SetProperty(ref _quantityRequired, value);
    }
}
