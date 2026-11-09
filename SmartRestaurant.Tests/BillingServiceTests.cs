using System;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class BillingServiceTests
{
    [Fact]
    public async Task ComputeInvoice_Statutory8PercentTax_MatchesFormula()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var dish = new MenuItem { Name = "Cá chẽm sốt chanh leo", Price = 100000m, IsAvailable = true };
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var order = new Order { TableId = 1, Status = "New" };
        order.OrderItems.Add(new OrderItem { MenuItemId = dish.MenuItemId, Quantity = 1, UnitPrice = 100000m });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new BillingService(context);

        // Act
        var invoice = await service.GenerateInvoiceAsync(order.OrderId, discountAmount: 0m);

        // Assert
        Assert.NotNull(invoice);
        Assert.Equal(0.08m, invoice.VatRate);
        Assert.Equal(8000m, invoice.VatAmount);
        Assert.Equal(108000m, invoice.TotalDue);
        Assert.Equal(108000m, order.FinalTotal);
    }

    [Fact]
    public async Task ComputeInvoice_WithValidDiscount_AppliesDiscountBeforeTax()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var dish = new MenuItem { Name = "Sườn nướng BBQ", Price = 200000m, IsAvailable = true };
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var order = new Order { TableId = 1, Status = "New" };
        order.OrderItems.Add(new OrderItem { MenuItemId = dish.MenuItemId, Quantity = 1, UnitPrice = 200000m });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new BillingService(context);

        // Act: Apply 50,000 VND discount
        // Taxable = 200,000 - 50,000 = 150,000 VND
        // VAT 8% = 12,000 VND
        // TotalDue = 162,000 VND
        var invoice = await service.GenerateInvoiceAsync(order.OrderId, discountAmount: 50000m);

        // Assert
        Assert.NotNull(invoice);
        Assert.Equal(12000m, invoice.VatAmount);
        Assert.Equal(162000m, invoice.TotalDue);
        Assert.Equal(50000m, order.DiscountAmount);
        Assert.Equal(162000m, order.FinalTotal);
    }

    [Fact]
    public async Task ComputeInvoice_DiscountGreaterThanSubTotal_ThrowsException()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var dish = new MenuItem { Name = "Mì Ý Carbonara", Price = 120000m, IsAvailable = true };
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var order = new Order { TableId = 1, Status = "New" };
        order.OrderItems.Add(new OrderItem { MenuItemId = dish.MenuItemId, Quantity = 1, UnitPrice = 120000m });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new BillingService(context);

        // Act & Assert (Discount 150,000 > SubTotal 120,000)
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GenerateInvoiceAsync(order.OrderId, discountAmount: 150000m));
    }

    [Fact]
    public async Task ComputeInvoice_NegativeDiscount_ThrowsException()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var dish = new MenuItem { Name = "Cơm Trưa", Price = 50000m };
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var order = new Order { TableId = 1, Status = "New" };
        order.OrderItems.Add(new OrderItem { MenuItemId = dish.MenuItemId, Quantity = 1, UnitPrice = 50000m });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new BillingService(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GenerateInvoiceAsync(order.OrderId, discountAmount: -5000m));
    }

    [Fact]
    public async Task ComputeInvoice_NonExistentOrder_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new BillingService(context);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GenerateInvoiceAsync(99999));
    }

    [Fact]
    public async Task ProcessPayment_ValidTender_MarksOrderCompletedAndTableCleaning()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "B-01", Capacity = 4, Status = TableStatus.Occupied, AreaId = 1 };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var order = new Order { TableId = table.TableId, Status = "Ready" };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var invoice = new Invoice
        {
            OrderId = order.OrderId,
            InvoiceCode = "INV-20261106-0004",
            VatRate = 0.08m,
            VatAmount = 16000m,
            TotalDue = 216000m,
            IssuedAt = DateTime.UtcNow
        };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var service = new BillingService(context);

        // Act
        var payment = await service.SettlePaymentAsync(invoice.InvoiceId, PaymentMethod.BankTransferQR, 216000m);

        // Assert
        Assert.NotNull(payment);
        Assert.Equal(216000m, payment.AmountPaid);
        Assert.Equal(PaymentMethod.BankTransferQR, payment.Method);

        var updatedOrder = await context.Orders.FindAsync(order.OrderId);
        var updatedTable = await context.DiningTables.FindAsync(table.TableId);

        Assert.Equal("Completed", updatedOrder!.Status);
        Assert.Equal(TableStatus.Cleaning, updatedTable!.Status);
    }

    [Fact]
    public async Task ProcessPayment_InsufficientAmountPaid_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var table = new DiningTable { TableName = "B-02", Capacity = 2, Status = TableStatus.Occupied, AreaId = 1 };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var dish = new MenuItem { Name = "Cơm Trưa", Price = 100000m };
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var order = new Order { TableId = table.TableId, Status = "Active" };
        order.OrderItems.Add(new OrderItem { MenuItemId = dish.MenuItemId, Quantity = 1, UnitPrice = 100000m });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new BillingService(context);
        var invoice = await service.GenerateInvoiceAsync(order.OrderId);

        // Act & Assert (Paid 1,000 < TotalDue 108,000)
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SettlePaymentAsync(invoice.InvoiceId, PaymentMethod.Cash, 1000m));
    }

    [Fact]
    public async Task ProcessPayment_NonExistentInvoice_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new BillingService(context);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SettlePaymentAsync(99999, PaymentMethod.Cash, 50000m));
    }
}
