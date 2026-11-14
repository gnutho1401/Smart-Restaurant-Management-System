using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Core.Security;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

public class StaffItemViewModel : ViewModelBase
{
    private UserRole _role;

    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    public UserRole Role
    {
        get => _role;
        set
        {
            if (SetProperty(ref _role, value))
            {
                OnPropertyChanged(nameof(RoleBadgeText));
                OnPropertyChanged(nameof(RoleBadgeColor));
            }
        }
    }

    public string RoleBadgeText => Role switch
    {
        UserRole.Admin => "ADMIN",
        UserRole.StoreManager => "QUẢN LÝ",
        UserRole.Cashier => "THU NGÂN",
        UserRole.HeadChef => "BẾP TRƯỞNG",
        _ => "NHÂN VIÊN"
    };

    public string RoleBadgeColor => Role switch
    {
        UserRole.Admin => "#E74C3C",
        UserRole.StoreManager => "#8E44AD",
        UserRole.Cashier => "#F39C12",
        UserRole.HeadChef => "#27AE60",
        _ => "#7F8C8D"
    };
}

/// <summary>
/// ViewModel managing the restaurant employee roster, role delegations, and credentials.
/// Strictly enforces RoleHierarchy: subordinates can NEVER modify, demote, or delete superiors or peers.
/// </summary>
public class StaffManagementViewModel : ViewModelBase
{
    private readonly IStaffService _staffService;
    private readonly IAuthService _authService;

    private ObservableCollection<StaffItemViewModel> _staffList = new();
    private StaffItemViewModel? _selectedStaff;

    private string _newUsername = string.Empty;
    private string _newPassword = string.Empty;
    private string _newFullName = string.Empty;
    private UserRole _newRole = UserRole.Cashier;

    private string _statusMessage = string.Empty;
    private bool _isSuccessMessage;
    private bool _isBusy;

    public ObservableCollection<StaffItemViewModel> StaffList
    {
        get => _staffList;
        set => SetProperty(ref _staffList, value);
    }

    public StaffItemViewModel? SelectedStaff
    {
        get => _selectedStaff;
        set
        {
            if (SetProperty(ref _selectedStaff, value))
            {
                OnPropertyChanged(nameof(CanModifySelectedStaff));
                OnPropertyChanged(nameof(IsSuperiorOrPeerSelected));
            }
        }
    }

    /// <summary>
    /// Guard property: Can only modify or delete accounts with STRICTLY LOWER rank than current user.
    /// Subordinates can NEVER modify superiors or peers!
    /// </summary>
    public bool CanModifySelectedStaff =>
        SelectedStaff != null &&
        _authService.CurrentUser != null &&
        SelectedStaff.UserId != _authService.CurrentUser.UserId &&
        RoleHierarchy.CanManage(_authService.CurrentUser.Role, SelectedStaff.Role);

    public bool IsSuperiorOrPeerSelected =>
        SelectedStaff != null &&
        _authService.CurrentUser != null &&
        !RoleHierarchy.CanManage(_authService.CurrentUser.Role, SelectedStaff.Role);

    public string NewUsername
    {
        get => _newUsername;
        set => SetProperty(ref _newUsername, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public string NewFullName
    {
        get => _newFullName;
        set => SetProperty(ref _newFullName, value);
    }

    public UserRole NewRole
    {
        get => _newRole;
        set => SetProperty(ref _newRole, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSuccessMessage
    {
        get => _isSuccessMessage;
        set => SetProperty(ref _isSuccessMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<UserRole> AvailableRoles { get; } = new();

    public IAsyncRelayCommand LoadStaffCommand { get; }
    public IAsyncRelayCommand CreateStaffCommand { get; }
    public IAsyncRelayCommand<UserRole> ChangeRoleCommand { get; }
    public IAsyncRelayCommand DeleteStaffCommand { get; }

    public StaffManagementViewModel(IStaffService staffService, IAuthService authService)
    {
        _staffService = staffService;
        _authService = authService;

        LoadStaffCommand = new AsyncRelayCommand(LoadStaffAsync);
        CreateStaffCommand = new AsyncRelayCommand(CreateStaffAsync);
        ChangeRoleCommand = new AsyncRelayCommand<UserRole>(ChangeRoleAsync);
        DeleteStaffCommand = new AsyncRelayCommand(DeleteStaffAsync);
    }

    public async Task LoadStaffAsync()
    {
        IsBusy = true;
        try
        {
            var raw = await _staffService.GetAllStaffAsync();
            var items = raw.Select(u => new StaffItemViewModel
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role
            }).ToList();

            StaffList = new ObservableCollection<StaffItemViewModel>(items);

            // Populate assignable roles: Actor can ONLY assign roles strictly lower than their own rank
            AvailableRoles.Clear();
            var currentRole = _authService.CurrentUser?.Role ?? UserRole.Cashier;
            foreach (var r in Enum.GetValues<UserRole>())
            {
                if (RoleHierarchy.CanManage(currentRole, r))
                {
                    AvailableRoles.Add(r);
                }
            }
            if (AvailableRoles.Any())
            {
                NewRole = AvailableRoles.First();
            }

            OnPropertyChanged(nameof(CanModifySelectedStaff));
            OnPropertyChanged(nameof(IsSuperiorOrPeerSelected));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi tải danh sách: {ex.Message}";
            IsSuccessMessage = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CreateStaffAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = "Vui lòng nhập tên đăng nhập và mật khẩu.";
            IsSuccessMessage = false;
            return;
        }

        var currentRole = _authService.CurrentUser?.Role ?? UserRole.Cashier;
        if (!RoleHierarchy.CanManage(currentRole, NewRole))
        {
            StatusMessage = $"❌ Cấp bậc không hợp lệ: Bạn không có quyền khởi tạo tài khoản có chức vụ '{NewRole}' ngang bằng hoặc cao hơn chức vụ của bạn.";
            IsSuccessMessage = false;
            return;
        }

        IsBusy = true;
        try
        {
            var user = await _staffService.CreateStaffAsync(NewUsername, NewPassword, NewFullName, NewRole);
            StatusMessage = $"✅ Đã thêm nhân viên '{user.FullName}' ({user.Username}) thành công!";
            IsSuccessMessage = true;

            NewUsername = string.Empty;
            NewPassword = string.Empty;
            NewFullName = string.Empty;

            await LoadStaffAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi thêm nhân viên: {ex.Message}";
            IsSuccessMessage = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ChangeRoleAsync(UserRole newRole)
    {
        if (SelectedStaff == null)
        {
            StatusMessage = "Vui lòng chọn nhân viên cần đổi vai trò.";
            IsSuccessMessage = false;
            return;
        }

        var currentRole = _authService.CurrentUser?.Role ?? UserRole.Cashier;

        // Subordinate cannot interfere with superior or peer
        if (!RoleHierarchy.CanManage(currentRole, SelectedStaff.Role))
        {
            StatusMessage = $"❌ Cấp dưới không thể can thiệp hoặc thay đổi chức vụ của cấp trên/cùng cấp ({SelectedStaff.FullName}).";
            IsSuccessMessage = false;
            return;
        }

        // Subordinate cannot elevate anyone to equal or higher role than self
        if (!RoleHierarchy.CanManage(currentRole, newRole))
        {
            StatusMessage = $"❌ Bạn không có quyền phân bổ vai trò '{newRole}' ngang bằng hoặc cao hơn cấp bậc của bạn.";
            IsSuccessMessage = false;
            return;
        }

        try
        {
            await _staffService.UpdateStaffRoleAsync(SelectedStaff.UserId, newRole);
            SelectedStaff.Role = newRole;
            StatusMessage = $"✅ Đã cập nhật vai trò nhân viên {SelectedStaff.FullName} thành '{newRole}'!";
            IsSuccessMessage = true;
            OnPropertyChanged(nameof(CanModifySelectedStaff));
            OnPropertyChanged(nameof(IsSuperiorOrPeerSelected));
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi đổi vai trò: {ex.Message}";
            IsSuccessMessage = false;
        }
    }

    private async Task DeleteStaffAsync()
    {
        if (SelectedStaff == null)
        {
            StatusMessage = "Vui lòng chọn nhân viên cần xóa.";
            IsSuccessMessage = false;
            return;
        }

        var currentRole = _authService.CurrentUser?.Role ?? UserRole.Cashier;

        // Subordinate cannot delete superior or peer
        if (!RoleHierarchy.CanManage(currentRole, SelectedStaff.Role))
        {
            StatusMessage = $"❌ Cấp dưới không thể xóa tài khoản của cấp trên hoặc cùng cấp ({SelectedStaff.FullName}).";
            IsSuccessMessage = false;
            return;
        }

        try
        {
            await _staffService.DeleteStaffAsync(SelectedStaff.UserId);
            StatusMessage = $"✅ Đã xóa nhân viên '{SelectedStaff.FullName}'!";
            IsSuccessMessage = true;
            SelectedStaff = null;
            await LoadStaffAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Lỗi xóa nhân viên: {ex.Message}";
            IsSuccessMessage = false;
        }
    }
}

