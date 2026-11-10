using System.Windows;
using System.Windows.Input;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for LoginWindow.xaml
/// </summary>
public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Allow window dragging
        MouseDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}

