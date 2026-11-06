using System.Windows;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for AiChatWindow.xaml
/// </summary>
public partial class AiChatWindow : Window
{
    public AiChatWindow(AiChatViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
