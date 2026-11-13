using System;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Cashier work shift and end-of-shift Z-Report reconciliation entity.
/// Invariant: CashVariance = ActualCashEnd - (OpeningFloat + CashSales).
/// </summary>
public class WorkShift
{
    public int ShiftId { get; set; }
    public int UserId { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal QrSales { get; set; }
    public decimal TotalSales { get; set; }
    public decimal? ActualCashEnd { get; set; }
    public decimal? CashVariance { get; set; }
    public bool IsClosed { get; set; }
    public string? Note { get; set; }

    public virtual User? User { get; set; }
}
