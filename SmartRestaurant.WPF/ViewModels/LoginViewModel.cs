using System;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.WPF.Common;

namespace SmartRestaurant.WPF.ViewModels;

/// <summary>
/// ViewModel managing staff authentication, credential validation,
/// and quick-switch demo personas for presentation testing.
/// </summary>
public class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;

    private string _username = "admin";
    private string _password = "admin123";
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public IAsyncRelayCommand LoginCommand { get; }
    public RelayCommand<string> QuickSelectUserCommand { get; }

    public event Action<User>? LoginSuccess;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;

        LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync);
        QuickSelectUserCommand = new RelayCommand<string>(ExecuteQuickSelect);
    }

    private async Task ExecuteLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var user = await _authService.LoginAsync(Username, Password);
            if (user == null)
            {
                ErrorMessage = "Tên đăng nhập hoặc mật khẩu không chính xác.";
                return;
            }

            LoginSuccess?.Invoke(user);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi hệ thống đăng nhập: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ExecuteQuickSelect(string? roleKey)
    {
        switch (roleKey)
        {
            case "Admin":
                Username = "admin";
                Password = "admin123";
                break;
            case "Manager":
                Username = "manager";
                Password = "manager123";
                break;
            case "Cashier":
                Username = "cashier";
                Password = "cashier123";
                break;
            case "Chef":
                Username = "chef";
                Password = "chef123";
                break;
        }
    }
}

