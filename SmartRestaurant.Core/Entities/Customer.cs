using System;
using System.Collections.Generic;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Loyalty member customer entity identified by a strictly unique phone number.
/// </summary>
public class Customer
{
    public int CustomerId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty; // Strictly unique
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int LoyaltyPoints { get; set; }
    public MembershipTier Tier { get; set; } = MembershipTier.Standard;
    public decimal TotalSpent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
