using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;

namespace SmartRestaurant.Services;

/// <summary>
/// Service executing financial settlement complying with Vietnamese statutory 8% VAT regulations.
/// Invariant: Taxable = Max(0, SubTotal - Discount); VAT = Round(Taxable * 0.08, 2, AwayFromZero); TotalDue = Taxable + VAT.
/// Once settled, table transitions to Cleaning state.
/// </summary>
public class BillingService : IBillingService
{
    private readonly RestaurantDbContext _context;

    public BillingService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<Invoice> GenerateInvoiceAsync(int orderId, decimal discountAmount = 0)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.Invoice)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order ID {orderId} không tồn tại.");
        }

        if (discountAmount < 0 || discountAmount > order.SubTotal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountAmount),
                $"Số tiền chiết khấu không hợp lệ: {discountAmount:N0} VND (Tạm tính: {order.SubTotal:N0} VND).");
        }

        // Financial Invariant Computation
        decimal subTotal = order.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity);
        decimal taxableAmount = Math.Max(0, subTotal - discountAmount);
        decimal vatAmount = Math.Round(taxableAmount * 0.0800m, 2, MidpointRounding.AwayFromZero);
        decimal totalDue = taxableAmount + vatAmount;

        var invoice = order.Invoice;
        if (invoice == null)
        {
            invoice = new Invoice
            {
                OrderId = order.OrderId,
                InvoiceCode = $"INV-{DateTime.UtcNow:yyyyMMdd}-{order.OrderId:D4}",
                VatRate = 0.08m,
                VatAmount = vatAmount,
                TotalDue = totalDue,
                IssuedAt = DateTime.UtcNow
            };
            await _context.Invoices.AddAsync(invoice);
        }
        else
        {
            invoice.VatRate = 0.08m;
            invoice.VatAmount = vatAmount;
            invoice.TotalDue = totalDue;
            invoice.IssuedAt = DateTime.UtcNow;
        }

        order.SubTotal = subTotal;
        order.DiscountAmount = discountAmount;
        order.FinalTotal = totalDue;

        await _context.SaveChangesAsync();
        return invoice;
    }

    public async Task<Payment> SettlePaymentAsync(int invoiceId, PaymentMethod method, decimal amountPaid)
    {
        var invoice = await _context.Invoices
            .Include(inv => inv.Order)
                .ThenInclude(o => o!.Table)
            .Include(inv => inv.Payments)
            .FirstOrDefaultAsync(inv => inv.InvoiceId == invoiceId);

        if (invoice == null)
        {
            throw new KeyNotFoundException($"Invoice ID {invoiceId} không tồn tại.");
        }

        if (amountPaid < invoice.TotalDue)
        {
            throw new InvalidOperationException(
                $"Số tiền thanh toán không đủ. Đã trả: {amountPaid:N0} VND, Cần thanh toán: {invoice.TotalDue:N0} VND.");
        }

        var payment = new Payment
        {
            InvoiceId = invoice.InvoiceId,
            Method = method,
            AmountPaid = amountPaid,
            PaidAt = DateTime.UtcNow
        };

        invoice.Payments.Add(payment);

        if (invoice.Order != null)
        {
            invoice.Order.Status = "Completed";

            // DFA Table State Machine: Occupied -> Cleaning
            if (invoice.Order.Table != null && invoice.Order.Table.Status == TableStatus.Occupied)
            {
                invoice.Order.Table.Status = TableStatus.Cleaning;
            }
        }

        await _context.SaveChangesAsync();
        return payment;
    }
}
