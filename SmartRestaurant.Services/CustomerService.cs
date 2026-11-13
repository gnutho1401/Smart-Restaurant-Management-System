using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;

namespace SmartRestaurant.Services;

/// <summary>
/// Service managing customer membership registration, unique phone numbers, and point rewards.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly RestaurantDbContext _context;

    public CustomerService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> FindByPhoneAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        string normalized = phone.Trim().Replace(" ", "").Replace("-", "");
        return await _context.Customers
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.PhoneNumber == normalized);
    }

    public Task<Customer?> GetCustomerByPhoneAsync(string phone) => FindByPhoneAsync(phone);

    public async Task<Customer?> GetCustomerByIdAsync(int customerId)
    {
        return await _context.Customers
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
    }

    public async Task<Customer> RegisterCustomerAsync(string phone, string fullName, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("Số điện thoại không được để trống.", nameof(phone));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Họ và tên khách hàng không được để trống.", nameof(fullName));
        }

        string normalized = phone.Trim().Replace(" ", "").Replace("-", "");
        bool exists = await _context.Customers.AnyAsync(c => c.PhoneNumber == normalized);
        if (exists)
        {
            throw new InvalidOperationException($"Số điện thoại '{phone}' đã được đăng ký thành viên trước đó.");
        }

        var customer = new Customer
        {
            PhoneNumber = normalized,
            FullName = fullName.Trim(),
            Email = email?.Trim(),
            LoyaltyPoints = 0,
            Tier = MembershipTier.Standard,
            TotalSpent = 0,
            CreatedAt = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return customer;
    }

    public async Task<bool> AddLoyaltyPointsAsync(int customerId, decimal spendAmount)
    {
        if (spendAmount <= 0) return false;

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer == null) return false;

        customer.TotalSpent += spendAmount;

        // Earning rate: 10,000 VND spent = 1 loyalty point
        int earnedPoints = (int)(spendAmount / 10000m);
        customer.LoyaltyPoints += earnedPoints;

        // Auto-recalculate membership tier
        customer.Tier = customer.LoyaltyPoints switch
        {
            >= 1000 => MembershipTier.Diamond,
            >= 500 => MembershipTier.Gold,
            >= 100 => MembershipTier.Silver,
            _ => MembershipTier.Standard
        };

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RedeemLoyaltyPointsAsync(int customerId, int pointsToRedeem)
    {
        if (pointsToRedeem <= 0) return false;

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer == null || customer.LoyaltyPoints < pointsToRedeem)
        {
            return false;
        }

        customer.LoyaltyPoints -= pointsToRedeem;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<Customer>> GetAllCustomersAsync()
    {
        return await _context.Customers
            .OrderByDescending(c => c.LoyaltyPoints)
            .ThenBy(c => c.FullName)
            .ToListAsync();
    }
}
