using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.WPF.Common;
using SmartRestaurant.WPF.Views;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// Master ViewModel orchestrating application navigation, sub-module lifecycle, and dialog launches.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private object? _currentView;
    private string _activeTab = "FloorPlan";

    public FloorPlanViewModel FloorPlanVm { get; }
    public PosOrderViewModel PosOrderVm { get; }
    public KitchenKdsViewModel KitchenKdsVm { get; }
    public MenuViewModel MenuVm { get; }

    public object? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public string ActiveTab
    {
        get => _activeTab;
        set => SetProperty(ref _activeTab, value);
    }

    public string CurrentUser { get; } = "Hồ Thăng Tùng (Trưởng Nhóm - DE190479)";

    public RelayCommand NavFloorPlanCommand { get; }
    public RelayCommand NavPosCommand { get; }
    public RelayCommand NavKdsCommand { get; }
    public RelayCommand NavMenuCommand { get; }
    public RelayCommand OpenBillingCommand { get; }
    public RelayCommand OpenAiAssistantCommand { get; }

    public MainViewModel(
        IServiceProvider serviceProvider,
        FloorPlanViewModel floorPlanVm,
        PosOrderViewModel posOrderVm,
        KitchenKdsViewModel kitchenKdsVm,
        MenuViewModel menuVm)
    {
        _serviceProvider = serviceProvider;
        FloorPlanVm = floorPlanVm;
        PosOrderVm = posOrderVm;
        KitchenKdsVm = kitchenKdsVm;
        MenuVm = menuVm;

        // Wire cross-view navigation events
        FloorPlanVm.RequestOpenPos += OnOpenPosRequested;
        PosOrderVm.RequestClosePos += () => SwitchView(FloorPlanVm, "FloorPlan");

        NavFloorPlanCommand = new RelayCommand(async () =>
        {
            SwitchView(FloorPlanVm, "FloorPlan");
            await FloorPlanVm.LoadTablesAsync();
        });

        NavPosCommand = new RelayCommand(() => SwitchView(PosOrderVm, "POS"));
        NavKdsCommand = new RelayCommand(async () =>
        {
            SwitchView(KitchenKdsVm, "KDS");
            await KitchenKdsVm.LoadTicketsAsync();
        });

        NavMenuCommand = new RelayCommand(async () =>
        {
            SwitchView(MenuVm, "Menu");
            await MenuVm.LoadMenuAsync();
        });

        OpenBillingCommand = new RelayCommand(OpenBillingModal);
        OpenAiAssistantCommand = new RelayCommand(OpenAiChatModal);

        // Start on Floor Plan
        SwitchView(FloorPlanVm, "FloorPlan");
        _ = FloorPlanVm.LoadTablesAsync();
    }

    private void SwitchView(object view, string tabName)
    {
        CurrentView = view;
        ActiveTab = tabName;
    }

    private async void OnOpenPosRequested(DiningTable table)
    {
        SwitchView(PosOrderVm, "POS");
        await PosOrderVm.InitializeAsync(table);
    }

    private void OpenBillingModal()
    {
        var billingVm = _serviceProvider.GetRequiredService<BillingViewModel>();
        // Check active table or default to table 3 (seeded with active orders)
        int orderId = PosOrderVm.CurrentTable?.TableId ?? 3;
        _ = billingVm.InitializeWithOrderAsync(orderId);

        var dialog = new BillingDialog
        {
            DataContext = billingVm,
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    private void OpenAiChatModal()
    {
        var aiChatVm = _serviceProvider.GetRequiredService<AiChatViewModel>();
        var aiWindow = new AiChatWindow(aiChatVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        aiWindow.Show();
    }
}
