using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
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
        UserRole.Admin => "👑 ADMIN",
        UserRole.StoreManager => "👔 QUẢN LÝ",
        UserRole.Cashier => "💵 THU NGÂN",
        UserRole.HeadChef => "👨‍🍳 BẾP TRƯỞNG",
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
/// </summary>
public class StaffManagementViewModel : ViewModelBase
{
    private readonly IStaffService _staffService;

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
        set => SetProperty(ref _selectedStaff, value);
    }

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

    public List<UserRole> AvailableRoles { get; } = new()
    {
        UserRole.Admin,
        UserRole.StoreManager,
        UserRole.Cashier,
        UserRole.HeadChef
    };

    public IAsyncRelayCommand LoadStaffCommand { get; }
    public IAsyncRelayCommand CreateStaffCommand { get; }
    public IAsyncRelayCommand<UserRole> ChangeRoleCommand { get; }
    public IAsyncRelayCommand DeleteStaffCommand { get; }

    public StaffManagementViewModel(IStaffService staffService)
    {
        _staffService = staffService;

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

        try
        {
            await _staffService.UpdateStaffRoleAsync(SelectedStaff.UserId, newRole);
            SelectedStaff.Role = newRole;
            StatusMessage = $"✅ Đã cập nhật vai trò nhân viên {SelectedStaff.FullName} thành '{newRole}'!";
            IsSuccessMessage = true;
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
