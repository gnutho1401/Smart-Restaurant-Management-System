using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SmartRestaurant.WPF.ViewModels;
using SmartRestaurant.WPF.Views;

namespace SmartRestaurant.WPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestLogout += OnRequestLogout;
    }

    private void OnRequestLogout()
    {
        var loginWindow = App.ServiceProvider.GetRequiredService<LoginWindow>();
        loginWindow.Show();
        this.Close();
    }
}
