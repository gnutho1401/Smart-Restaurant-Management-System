using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Domain authentication contract handling user login, session lifecycle, and Role-Based Access Control (RBAC).
/// </summary>
public interface IAuthService
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    Task<User?> LoginAsync(string username, string password);
    void Logout();
    bool HasPermission(params UserRole[] allowedRoles);
}

