using System.Windows.Controls;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for ReportsView.xaml
/// </summary>
public partial class ReportsView : UserControl
{
    public ReportsView(ReportsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (s, e) => await viewModel.LoadDashboardAsync();
    }
}
