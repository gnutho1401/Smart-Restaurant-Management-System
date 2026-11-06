using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Dining table entity with optimistic concurrency token and DFA state tracking.
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
}
