using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Service contract for statutory 8% VAT calculations and payment settlements.
/// </summary>
public interface IBillingService
{
    Task<Invoice> GenerateInvoiceAsync(int orderId, decimal discountAmount = 0);
    Task<Payment> SettlePaymentAsync(int invoiceId, PaymentMethod method, decimal amountPaid, string? idempotencyKey = null);
}
