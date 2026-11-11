using System;
using SmartRestaurant.Core.ValueObjects;
using Xunit;

namespace SmartRestaurant.Tests;

public class VatCalculationTests
{
    [Fact]
    public void Compute_StandardSubTotalWithoutDiscount_CalculatesStatutory8PercentTaxAccurately()
    {
        // Arrange
        decimal subTotal = 1_000_000m;
        decimal discount = 0m;

        // Act
        var calc = VatCalculation.Compute(subTotal, discount);

        // Assert
        Assert.Equal(1_000_000m, calc.SubTotal);
        Assert.Equal(0m, calc.DiscountAmount);
        Assert.Equal(1_000_000m, calc.TaxableAmount);
        Assert.Equal(0.08m, calc.VatRate);
        Assert.Equal(80_000m, calc.VatAmount);
        Assert.Equal(1_080_000m, calc.TotalDue);
    }

    [Fact]
    public void Compute_ValidDiscount_AppliesDiscountBeforeTax()
    {
        // Arrange: SubTotal 500,000 VND, Discount 100,000 VND
        // Taxable = 400,000 VND -> VAT 8% = 32,000 VND -> TotalDue = 432,000 VND
        decimal subTotal = 500_000m;
        decimal discount = 100_000m;

        // Act
        var calc = VatCalculation.Compute(subTotal, discount);

        // Assert
        Assert.Equal(400_000m, calc.TaxableAmount);
        Assert.Equal(32_000m, calc.VatAmount);
        Assert.Equal(432_000m, calc.TotalDue);
    }

    [Fact]
    public void Compute_DiscountGreaterThanSubTotal_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        decimal subTotal = 200_000m;
        decimal discount = 250_000m;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => VatCalculation.Compute(subTotal, discount));
    }

    [Fact]
    public void Compute_NegativeSubTotal_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => VatCalculation.Compute(-50_000m, 0m));
    }

    [Fact]
    public void Compute_NegativeDiscount_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => VatCalculation.Compute(100_000m, -10_000m));
    }
}

