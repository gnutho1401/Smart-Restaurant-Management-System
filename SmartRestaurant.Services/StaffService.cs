using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Core.Security;
using SmartRestaurant.Data;

namespace SmartRestaurant.Services;

/// <summary>
/// Service managing restaurant employee roster, credential updates, and role assignments.
/// </summary>
public class StaffService : IStaffService
{
    private readonly RestaurantDbContext _context;

    public StaffService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<User>> GetAllStaffAsync()
    {
        return await _context.Users
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<User> CreateStaffAsync(string username, string password, string fullName, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Tên đăng nhập và mật khẩu không được để trống.");
        }

        string normalized = username.Trim().ToLower();
        bool exists = await _context.Users.AnyAsync(u => u.Username.ToLower() == normalized);
        if (exists)
        {
            throw new InvalidOperationException($"Tên đăng nhập '{username}' đã tồn tại trong hệ thống.");
        }

        var user = new User
        {
            Username = username.Trim(),
            PasswordHash = PasswordHasher.HashPassword(password),
            FullName = string.IsNullOrWhiteSpace(fullName) ? username : fullName.Trim(),
            Role = role
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<bool> UpdateStaffRoleAsync(int userId, UserRole newRole)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return false;

        // Invariant: Subordinates or peers cannot demote Admin
        if (user.Role == UserRole.Admin && newRole != UserRole.Admin)
        {
            throw new InvalidOperationException("Không thể hạ quyền tài khoản Quản trị viên (Admin).");
        }

        user.Role = newRole;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetStaffPasswordAsync(int userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new ArgumentException("Mật khẩu mới không được để trống.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return false;

        user.PasswordHash = PasswordHasher.HashPassword(newPassword);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteStaffAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return false;

        // Invariant: Admin accounts are protected from deletion
        if (user.Role == UserRole.Admin)
        {
            throw new InvalidOperationException("Không thể xóa tài khoản Quản trị viên (Admin).");
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }
}

