using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;
using SmartRestaurant.WPF.Common;
using SmartRestaurant.WPF.Views;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// Master ViewModel orchestrating application navigation, sub-module lifecycle,
/// and Role-Based Access Control (RBAC) permission boundaries.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthService _authService;
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

    // Authenticated User & Role Information
    public string CurrentUserName => _authService.CurrentUser?.FullName ?? "Nhân Viên";
    public string CurrentUserRole => _authService.CurrentUser?.Role.ToString() ?? "Cashier";

    public string RoleBadgeText => _authService.CurrentUser?.Role switch
    {
        UserRole.Admin => "👑 ADMIN",
        UserRole.StoreManager => "👔 QUẢN LÝ",
        UserRole.Cashier => "💵 THU NGÂN",
        UserRole.HeadChef => "👨‍🍳 BẾP TRƯỞNG",
        _ => "NHÂN VIÊN"
    };

    public string RoleBadgeColor => _authService.CurrentUser?.Role switch
    {
        UserRole.Admin => "#E74C3C",        // Vivid Red
        UserRole.StoreManager => "#8E44AD", // Deep Purple
        UserRole.Cashier => "#F39C12",      // Warm Amber
        UserRole.HeadChef => "#27AE60",     // Emerald Green
        _ => "#7F8C8D"
    };

    // Role-Based Access Control (RBAC) Permissions
    public bool IsFloorPlanAllowed => _authService.HasPermission(UserRole.Admin, UserRole.StoreManager, UserRole.Cashier);
    public bool IsPosAllowed => _authService.HasPermission(UserRole.Admin, UserRole.StoreManager, UserRole.Cashier);
    public bool IsKdsAllowed => _authService.HasPermission(UserRole.Admin, UserRole.StoreManager, UserRole.HeadChef);
    public bool IsMenuAllowed => _authService.HasPermission(UserRole.Admin, UserRole.StoreManager, UserRole.HeadChef);
    public bool IsBillingAllowed => _authService.HasPermission(UserRole.Admin, UserRole.StoreManager, UserRole.Cashier);
    public bool IsAiAssistantAllowed => true;

    public RelayCommand NavFloorPlanCommand { get; }
    public RelayCommand NavPosCommand { get; }
    public RelayCommand NavKdsCommand { get; }
    public RelayCommand NavMenuCommand { get; }
    public RelayCommand OpenBillingCommand { get; }
    public RelayCommand OpenAiAssistantCommand { get; }
    public RelayCommand LogoutCommand { get; }

    public event Action? RequestLogout;

    public MainViewModel(
        IServiceProvider serviceProvider,
        IAuthService authService,
        FloorPlanViewModel floorPlanVm,
        PosOrderViewModel posOrderVm,
        KitchenKdsViewModel kitchenKdsVm,
        MenuViewModel menuVm)
    {
        _serviceProvider = serviceProvider;
        _authService = authService;
        FloorPlanVm = floorPlanVm;
        PosOrderVm = posOrderVm;
        KitchenKdsVm = kitchenKdsVm;
        MenuVm = menuVm;

        // Wire cross-view navigation events
        FloorPlanVm.RequestOpenPos += OnOpenPosRequested;
        FloorPlanVm.RequestOpenBilling += OnOpenBillingForTableRequested;
        PosOrderVm.RequestClosePos += () => SwitchView(FloorPlanVm, "FloorPlan");

        NavFloorPlanCommand = new RelayCommand(async () =>
        {
            if (!IsFloorPlanAllowed) return;
            SwitchView(FloorPlanVm, "FloorPlan");
            await FloorPlanVm.LoadTablesAsync();
        });

        NavPosCommand = new RelayCommand(() =>
        {
            if (!IsPosAllowed) return;
            SwitchView(PosOrderVm, "POS");
        });

        NavKdsCommand = new RelayCommand(async () =>
        {
            if (!IsKdsAllowed) return;
            SwitchView(KitchenKdsVm, "KDS");
            await KitchenKdsVm.LoadTicketsAsync();
        });

        NavMenuCommand = new RelayCommand(async () =>
        {
            if (!IsMenuAllowed) return;
            SwitchView(MenuVm, "Menu");
            await MenuVm.LoadMenuAsync();
        });

        OpenBillingCommand = new RelayCommand(OpenBillingModal);
        OpenAiAssistantCommand = new RelayCommand(OpenAiChatModal);
        LogoutCommand = new RelayCommand(ExecuteLogout);

        ApplyInitialRoleView();
    }

    public void ApplyInitialRoleView()
    {
        // Notify all permission properties in case user just logged in
        OnPropertyChanged(nameof(CurrentUserName));
        OnPropertyChanged(nameof(CurrentUserRole));
        OnPropertyChanged(nameof(RoleBadgeText));
        OnPropertyChanged(nameof(RoleBadgeColor));
        OnPropertyChanged(nameof(IsFloorPlanAllowed));
        OnPropertyChanged(nameof(IsPosAllowed));
        OnPropertyChanged(nameof(IsKdsAllowed));
        OnPropertyChanged(nameof(IsMenuAllowed));
        OnPropertyChanged(nameof(IsBillingAllowed));

        // Direct user to their primary operational screen upon login
        if (_authService.CurrentUser?.Role == UserRole.HeadChef)
        {
            SwitchView(KitchenKdsVm, "KDS");
            _ = KitchenKdsVm.LoadTicketsAsync();
        }
        else
        {
            SwitchView(FloorPlanVm, "FloorPlan");
            _ = FloorPlanVm.LoadTablesAsync();
        }
    }

    private void SwitchView(object view, string tabName)
    {
        CurrentView = view;
        ActiveTab = tabName;
    }

    private async void OnOpenPosRequested(DiningTable table)
    {
        if (!IsPosAllowed) return;
        SwitchView(PosOrderVm, "POS");
        await PosOrderVm.InitializeAsync(table);
    }

    private async void OpenBillingModal()
    {
        await OpenBillingForTableInternalAsync(null);
    }

    private async void OnOpenBillingForTableRequested(DiningTable table)
    {
        await OpenBillingForTableInternalAsync(table);
    }

    private async Task OpenBillingForTableInternalAsync(DiningTable? table)
    {
        if (!IsBillingAllowed)
        {
            System.Windows.MessageBox.Show(
                "Bạn không có quyền thực hiện quyết toán hóa đơn.\nChức năng này chỉ dành cho Thu Ngân, Quản Lý và Admin.",
                "Từ Chối Quyền Truy Cập (RBAC)",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var orderService = _serviceProvider.GetRequiredService<IOrderService>();
        Order? targetOrder = null;

        if (table != null)
        {
            targetOrder = await orderService.GetActiveOrderByTableIdAsync(table.TableId);
        }
        else if (PosOrderVm.CurrentTable != null)
        {
            targetOrder = await orderService.GetActiveOrderByTableIdAsync(PosOrderVm.CurrentTable.TableId);
        }
        else if (FloorPlanVm.SelectedTable != null)
        {
            targetOrder = await orderService.GetActiveOrderByTableIdAsync(FloorPlanVm.SelectedTable.TableId);
        }

        if (targetOrder == null)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();
            targetOrder = await db.Orders
                .Include(o => o.Table)
                .Where(o => o.Status == "Active")
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        if (targetOrder == null)
        {
            System.Windows.MessageBox.Show(
                "Hiện không có bàn nào đang có đơn hàng cần thanh toán.",
                "Thông Báo Quyết Toán",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var billingVm = _serviceProvider.GetRequiredService<BillingViewModel>();
        await billingVm.InitializeWithOrderAsync(targetOrder.OrderId);

        var dialog = new BillingDialog
        {
            DataContext = billingVm,
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowDialog();

        await FloorPlanVm.LoadTablesAsync();
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

    private void ExecuteLogout()
    {
        _authService.Logout();
        RequestLogout?.Invoke();
    }
}
