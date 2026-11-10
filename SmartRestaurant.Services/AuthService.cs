using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Core.Security;
using SmartRestaurant.Data;

namespace SmartRestaurant.Services;

/// <summary>
/// Authentication service implementing credential verification via PBKDF2 hashing
/// and centralized Role-Based Access Control (RBAC) permission enforcement.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly RestaurantDbContext? _context;
    private User? _currentUser;

    public User? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;

    public AuthService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public AuthService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        var normalizedUsername = username.Trim().ToLower();

        User? user;
        if (_context != null)
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername);
        }
        else if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();
            user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername);
        }
        else
        {
            return null;
        }

        if (user == null)
            return null;

        bool isValid = PasswordHasher.VerifyPassword(password, user.PasswordHash);
        if (!isValid)
            return null;

        _currentUser = user;
        return user;
    }

    public void Logout()
    {
        _currentUser = null;
    }

    public bool HasPermission(params UserRole[] allowedRoles)
    {
        if (_currentUser == null)
            return false;

        // Admin has universal superuser access across all modules
        if (_currentUser.Role == UserRole.Admin)
            return true;

        if (allowedRoles == null || allowedRoles.Length == 0)
            return true;

        return allowedRoles.Contains(_currentUser.Role);
    }
}
