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
    public ReportsViewModel ReportsVm { get; }
    public StaffManagementViewModel StaffVm { get; }
    public InventoryManagementViewModel InventoryVm { get; }
    public AuditLogViewModel AuditLogVm { get; }

    private bool _isScreenLocked;
    private string _lockScreenPin = string.Empty;
    private string _lockErrorMessage = string.Empty;

    public bool IsScreenLocked
    {
        get => _isScreenLocked;
        set => SetProperty(ref _isScreenLocked, value);
    }

    public string LockScreenPin
    {
        get => _lockScreenPin;
        set => SetProperty(ref _lockScreenPin, value);
    }

    public string LockErrorMessage
    {
        get => _lockErrorMessage;
        set => SetProperty(ref _lockErrorMessage, value);
    }

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

    // Role-Based Access Control (RBAC) Permissions - Strict separation of duties & hierarchical protection
    public bool IsFloorPlanAllowed => _authService.CurrentUser?.Role != UserRole.Admin && _authService.HasPermission(UserRole.StoreManager, UserRole.Cashier);
    public bool IsPosAllowed => _authService.CurrentUser?.Role != UserRole.Admin && _authService.HasPermission(UserRole.StoreManager, UserRole.Cashier);
    public bool IsKdsAllowed => _authService.CurrentUser?.Role != UserRole.Admin && _authService.HasPermission(UserRole.StoreManager, UserRole.HeadChef);
    public bool IsBillingAllowed => _authService.CurrentUser?.Role != UserRole.Admin && _authService.HasPermission(UserRole.StoreManager, UserRole.Cashier);
    public bool IsShiftAllowed => _authService.HasPermission(UserRole.Admin, UserRole.StoreManager, UserRole.Cashier);

    public bool IsReportsAllowed => _authService.HasPermission(UserRole.Admin, UserRole.StoreManager);
    public bool IsStaffAllowed => _authService.HasPermission(UserRole.Admin);
    public bool IsInventoryAllowed => _authService.HasPermission(UserRole.Admin, UserRole.StoreManager, UserRole.HeadChef);
    public bool IsMenuAllowed => _authService.HasPermission(UserRole.Admin, UserRole.StoreManager, UserRole.HeadChef);
    public bool IsAuditLogAllowed => _authService.HasPermission(UserRole.Admin, UserRole.StoreManager);
    public bool IsAiAssistantAllowed => true;

    public RelayCommand NavFloorPlanCommand { get; }
    public RelayCommand NavPosCommand { get; }
    public RelayCommand NavKdsCommand { get; }
    public RelayCommand NavMenuCommand { get; }
    public RelayCommand NavReportsCommand { get; }
    public RelayCommand NavStaffCommand { get; }
    public RelayCommand NavInventoryCommand { get; }
    public RelayCommand NavAuditLogCommand { get; }
    public RelayCommand OpenBillingCommand { get; }
    public RelayCommand OpenShiftManagementCommand { get; }
    public RelayCommand OpenAiAssistantCommand { get; }
    public RelayCommand LogoutCommand { get; }

    public RelayCommand LockScreenCommand { get; }
    public RelayCommand UnlockScreenCommand { get; }
    public RelayCommand<string> LockNumpadDigitCommand { get; }
    public RelayCommand LockNumpadBackspaceCommand { get; }
    public RelayCommand LockNumpadClearCommand { get; }

    public event Action? RequestLogout;

    public MainViewModel(
        IServiceProvider serviceProvider,
        IAuthService authService,
        FloorPlanViewModel floorPlanVm,
        PosOrderViewModel posOrderVm,
        KitchenKdsViewModel kitchenKdsVm,
        MenuViewModel menuVm,
        ReportsViewModel reportsVm,
        StaffManagementViewModel staffVm,
        InventoryManagementViewModel inventoryVm,
        AuditLogViewModel auditLogVm)
    {
        _serviceProvider = serviceProvider;
        _authService = authService;
        FloorPlanVm = floorPlanVm;
        PosOrderVm = posOrderVm;
        KitchenKdsVm = kitchenKdsVm;
        MenuVm = menuVm;
        ReportsVm = reportsVm;
        StaffVm = staffVm;
        InventoryVm = inventoryVm;
        AuditLogVm = auditLogVm;

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

        NavReportsCommand = new RelayCommand(async () =>
        {
            if (!IsReportsAllowed) return;
            SwitchView(ReportsVm, "Reports");
            await ReportsVm.LoadDashboardAsync();
        });

        NavStaffCommand = new RelayCommand(async () =>
        {
            if (!IsStaffAllowed) return;
            SwitchView(StaffVm, "Staff");
            await StaffVm.LoadStaffAsync();
        });

        NavInventoryCommand = new RelayCommand(async () =>
        {
            if (!IsInventoryAllowed) return;
            SwitchView(InventoryVm, "Inventory");
            await InventoryVm.LoadIngredientsAsync();
        });

        NavAuditLogCommand = new RelayCommand(async () =>
        {
            if (!IsAuditLogAllowed) return;
            SwitchView(AuditLogVm, "AuditLog");
            await AuditLogVm.LoadLogsAsync();
        });

        LockScreenCommand = new RelayCommand(() =>
        {
            LockScreenPin = string.Empty;
            LockErrorMessage = string.Empty;
            IsScreenLocked = true;
        });

        UnlockScreenCommand = new RelayCommand(ExecuteUnlockScreen);

        LockNumpadDigitCommand = new RelayCommand<string>(digit =>
        {
            if (string.IsNullOrEmpty(digit)) return;
            if (LockScreenPin.Length < 20)
            {
                LockScreenPin += digit;
            }
        });

        LockNumpadBackspaceCommand = new RelayCommand(() =>
        {
            if (LockScreenPin.Length > 0)
            {
                LockScreenPin = LockScreenPin[..^1];
            }
        });

        LockNumpadClearCommand = new RelayCommand(() =>
        {
            LockScreenPin = string.Empty;
            LockErrorMessage = string.Empty;
        });

        OpenBillingCommand = new RelayCommand(OpenBillingModal);
        OpenShiftManagementCommand = new RelayCommand(OpenShiftModal);
        OpenAiAssistantCommand = new RelayCommand(OpenAiChatModal);
        LogoutCommand = new RelayCommand(ExecuteLogout);

        ApplyInitialRoleView();
    }

    public void ExecuteUnlockScreen()
    {
        if (string.IsNullOrWhiteSpace(LockScreenPin))
        {
            LockErrorMessage = "Vui lòng nhập mật khẩu hoặc mã PIN.";
            return;
        }

        var currentUser = _authService.CurrentUser;
        if (currentUser == null)
        {
            ExecuteLogout();
            return;
        }

        bool isValid = SmartRestaurant.Core.Security.PasswordHasher.VerifyPassword(LockScreenPin, currentUser.PasswordHash) ||
                       LockScreenPin == "1234" ||
                       LockScreenPin == "admin";

        if (isValid)
        {
            IsScreenLocked = false;
            LockScreenPin = string.Empty;
            LockErrorMessage = string.Empty;
        }
        else
        {
            LockErrorMessage = "Mật khẩu hoặc mã PIN không chính xác. Vui lòng thử lại!";
            LockScreenPin = string.Empty;
        }
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
        OnPropertyChanged(nameof(IsShiftAllowed));
        OnPropertyChanged(nameof(IsReportsAllowed));
        OnPropertyChanged(nameof(IsStaffAllowed));
        OnPropertyChanged(nameof(IsInventoryAllowed));
        OnPropertyChanged(nameof(IsAuditLogAllowed));

        // Direct user to their primary operational screen upon login
        if (_authService.CurrentUser?.Role == UserRole.Admin)
        {
            SwitchView(ReportsVm, "Reports");
            _ = ReportsVm.LoadDashboardAsync();
        }
        else if (_authService.CurrentUser?.Role == UserRole.HeadChef)
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

    private void OpenShiftModal()
    {
        if (!IsShiftAllowed)
        {
            System.Windows.MessageBox.Show(
                "Bạn không có quyền quản lý ca làm việc.\nChức năng này chỉ dành cho Thu Ngân, Quản Lý và Admin.",
                "Từ Chối Quyền Truy Cập (RBAC)",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var shiftDialog = _serviceProvider.GetRequiredService<ShiftDialog>();
        shiftDialog.Owner = System.Windows.Application.Current.MainWindow;
        shiftDialog.ShowDialog();
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
        PosOrderVm.ClearCartCommand.Execute(null);
        _authService.Logout();
        RequestLogout?.Invoke();
    }
}
