using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Restaurant staff user entity.
/// </summary>
public class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Cashier;
}
