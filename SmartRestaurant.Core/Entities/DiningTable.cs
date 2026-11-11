using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Exceptions;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Dining table entity with optimistic concurrency token and DFA state tracking.
/// Encapsulates rich domain logic for table status transitions.
/// </summary>
public class DiningTable
{
    public int TableId { get; set; }
    public int AreaId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public TableStatus Status { get; set; } = TableStatus.Available;

    [ConcurrencyCheck]
    public byte[]? RowVersion { get; set; }

    public virtual DiningArea? Area { get; set; }
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    [NotMapped]
    public Order? ActiveOrder => Orders.FirstOrDefault(o => o.Status == "Active");

    [NotMapped]
    public string ActiveOrderSummary
    {
        get
        {
            var order = ActiveOrder;
            if (order == null || Status != TableStatus.Occupied) return string.Empty;
            int count = order.OrderItems.Sum(oi => oi.Quantity);
            decimal total = order.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity);
            return count > 0 ? $"🛒 {count} món • {total:N0} đ" : string.Empty;
        }
    }

    /// <summary>
    /// Checks if transitioning to the target status satisfies the Deterministic Finite Automaton (DFA) invariants.
    /// </summary>
    public bool CanTransitionTo(TableStatus targetStatus)
    {
        if (Status == targetStatus) return true;

        return (Status, targetStatus) switch
        {
            (TableStatus.Available, TableStatus.Occupied) => true,
            (TableStatus.Available, TableStatus.Reserved) => true,
            (TableStatus.Reserved, TableStatus.Occupied) => true,
            (TableStatus.Reserved, TableStatus.Available) => true,
            (TableStatus.Occupied, TableStatus.Cleaning) => true,
            (TableStatus.Cleaning, TableStatus.Available) => true,
            _ => false
        };
    }

    /// <summary>
    /// Transitions table to new status. Throws InvalidStatusTransitionException if transition violates DFA rules.
    /// </summary>
    public void TransitionTo(TableStatus targetStatus)
    {
        if (Status == targetStatus) return;

        if (!CanTransitionTo(targetStatus))
        {
            throw new InvalidStatusTransitionException(
                Status.ToString(),
                targetStatus.ToString(),
                $"DiningTable '{TableName}' (ID: {TableId})");
        }

        Status = targetStatus;
    }
}
