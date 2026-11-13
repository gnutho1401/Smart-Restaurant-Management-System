using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Service contract for loyalty member customer lifecycle, unique phone lookups, and point accrual/redemption.
/// </summary>
public interface ICustomerService
{
    Task<Customer?> FindByPhoneAsync(string phone);
    Task<Customer?> GetCustomerByPhoneAsync(string phone);
    Task<Customer?> GetCustomerByIdAsync(int customerId);
    Task<Customer> RegisterCustomerAsync(string phone, string fullName, string? email = null);
    Task<bool> AddLoyaltyPointsAsync(int customerId, decimal spendAmount);
    Task<bool> RedeemLoyaltyPointsAsync(int customerId, int pointsToRedeem);
    Task<IEnumerable<Customer>> GetAllCustomersAsync();
}
