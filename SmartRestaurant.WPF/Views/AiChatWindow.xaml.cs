using System.Collections.Specialized;
using System.Windows;
using SmartRestaurant.WPF.ViewModels;

namespace SmartRestaurant.WPF.Views;

/// <summary>
/// Interaction logic for AiChatWindow.xaml
/// </summary>
public partial class AiChatWindow : Window
{
    public AiChatWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public AiChatWindow(AiChatViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is AiChatViewModel vm)
        {
            vm.Messages.CollectionChanged += (s, ev) =>
            {
                if (ev.Action == NotifyCollectionChangedAction.Add)
                {
                    Dispatcher.InvokeAsync(() => ChatScrollViewer.ScrollToEnd());
                }
            };
        }
    }
}
