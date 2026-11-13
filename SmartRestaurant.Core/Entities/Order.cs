using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Master order header entity tracking active dining room orders and financial sums.
/// </summary>
public class Order
{
    public int OrderId { get; set; }
    public int TableId { get; set; }
    public int CreatedByUserId { get; set; }
    public int? CustomerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ConcurrencyCheck]
    public string Status { get; set; } = "Active"; // Active, Completed, Cancelled
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalTotal { get; set; }

    public virtual DiningTable? Table { get; set; }
    public virtual Customer? Customer { get; set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual Invoice? Invoice { get; set; }
}
