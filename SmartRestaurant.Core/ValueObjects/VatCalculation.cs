using System;

namespace SmartRestaurant.Core.ValueObjects;

/// <summary>
/// Domain Value Object enforcing Vietnamese statutory 8% VAT financial invariant.
/// Formulation: Taxable = Max(0, SubTotal - Discount); VAT = Round(Taxable * 0.08, 2, AwayFromZero); TotalDue = Taxable + VAT.
/// Guaranteed immutable and free from side effects.
/// </summary>
public readonly record struct VatCalculation
{
    public decimal SubTotal { get; }
    public decimal DiscountAmount { get; }
    public decimal TaxableAmount { get; }
    public decimal VatRate { get; }
    public decimal VatAmount { get; }
    public decimal TotalDue { get; }

    public const decimal StatutoryVatRate = 0.0800m;

    private VatCalculation(decimal subTotal, decimal discountAmount)
    {
        if (subTotal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(subTotal), "Tạm tính không thể là số âm.");
        }

        if (discountAmount < 0 || discountAmount > subTotal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountAmount),
                $"Số tiền chiết khấu không hợp lệ: {discountAmount:N0} VND (Tạm tính: {subTotal:N0} VND).");
        }

        SubTotal = subTotal;
        DiscountAmount = discountAmount;
        TaxableAmount = Math.Max(0, subTotal - discountAmount);
        VatRate = StatutoryVatRate;
        VatAmount = Math.Round(TaxableAmount * StatutoryVatRate, 2, MidpointRounding.AwayFromZero);
        TotalDue = TaxableAmount + VatAmount;
    }

    public static VatCalculation Compute(decimal subTotal, decimal discountAmount = 0)
    {
        return new VatCalculation(subTotal, discountAmount);
    }
}
