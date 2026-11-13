using System.Windows.Controls;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for ReportsView.xaml
/// </summary>
public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is ReportsViewModel vm)
            {
                await vm.LoadDashboardAsync();
            }
        };
    }

    public ReportsView(ReportsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}

