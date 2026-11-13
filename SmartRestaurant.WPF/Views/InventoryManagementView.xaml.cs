using System.Windows.Controls;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for InventoryManagementView.xaml
/// </summary>
public partial class InventoryManagementView : UserControl
{
    public InventoryManagementView(InventoryManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (s, e) => await viewModel.LoadIngredientsAsync();
    }
}
