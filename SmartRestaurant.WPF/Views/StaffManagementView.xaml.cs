using System.Windows.Controls;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for StaffManagementView.xaml
/// </summary>
public partial class StaffManagementView : UserControl
{
    public StaffManagementView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is StaffManagementViewModel vm)
            {
                await vm.LoadStaffAsync();
            }
        };
    }

    public StaffManagementView(StaffManagementViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}

