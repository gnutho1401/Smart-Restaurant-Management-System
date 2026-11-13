using System;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Payment transaction record associated with an invoice.
/// </summary>
public class Payment
{
    public int PaymentId { get; set; }
    public int InvoiceId { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public decimal AmountPaid { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    public virtual Invoice? Invoice { get; set; }
}
