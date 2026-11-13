using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Individual line item within an order, with kitchen preparation status and dietary notes.
/// </summary>
public class OrderItem
{
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public CookingStatus CookingStatus { get; set; } = CookingStatus.Pending;
    public string? Note { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    public virtual Order? Order { get; set; }
    public virtual MenuItem? MenuItem { get; set; }
}
