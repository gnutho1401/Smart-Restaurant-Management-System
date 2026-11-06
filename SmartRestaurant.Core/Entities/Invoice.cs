using System;
using System.Collections.Generic;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Financial invoice record with statutory 8% VAT and multi-tender payments.
/// </summary>
public class Invoice
{
    public int InvoiceId { get; set; }
    public int OrderId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public decimal VatRate { get; set; } = 0.08m; // 8% statutory VAT
    public decimal VatAmount { get; set; }
    public decimal TotalDue { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public virtual Order? Order { get; set; }
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
