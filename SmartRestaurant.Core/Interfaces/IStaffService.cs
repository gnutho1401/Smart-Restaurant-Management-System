using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Service contract for restaurant staff account management, onboarding, and role assignments.
/// </summary>
public interface IStaffService
{
    Task<IEnumerable<User>> GetAllStaffAsync();
    Task<User> CreateStaffAsync(string username, string password, string fullName, UserRole role);
    Task<bool> UpdateStaffRoleAsync(int userId, UserRole newRole);
    Task<bool> ResetStaffPasswordAsync(int userId, string newPassword);
    Task<bool> DeleteStaffAsync(int userId);
}
