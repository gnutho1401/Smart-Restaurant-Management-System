using System.Windows.Controls;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for StaffManagementView.xaml
/// </summary>
public partial class StaffManagementView : UserControl
{
    public StaffManagementView(StaffManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (s, e) => await viewModel.LoadStaffAsync();
    }
}

