using System.Windows.Controls;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for InventoryManagementView.xaml
/// </summary>
public partial class InventoryManagementView : UserControl
{
    public InventoryManagementView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is InventoryManagementViewModel vm)
            {
                await vm.LoadIngredientsAsync();
            }
        };
    }

    public InventoryManagementView(InventoryManagementViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}

