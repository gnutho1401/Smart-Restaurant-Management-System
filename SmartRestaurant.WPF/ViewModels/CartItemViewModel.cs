using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// Model representing an active item line inside the POS ordering cart.
/// </summary>
public class CartItemViewModel : ViewModelBase
{
    private int _quantity;
    private string? _allergyNote;

    public int MenuItemId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(LineTotal));
            }
        }
    }

    public decimal LineTotal => UnitPrice * Quantity;

    public string? AllergyNote
    {
        get => _allergyNote;
        set => SetProperty(ref _allergyNote, value);
    }
}
