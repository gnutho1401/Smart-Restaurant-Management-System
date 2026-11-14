using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Services;
using SmartRestaurant.WPF.ViewModels;
using Xunit;

namespace SmartRestaurant.Tests;

public class Phase2TouchAndErgonomicsTests
{
    [Fact]
    public void BillingViewModel_TouchNumpad_Digits_Backspace_Clear_WorksCorrectly()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var billingService = new BillingService(context);
        var orderService = new OrderService(context);
        var customerService = new CustomerService(context);

        var vm = new BillingViewModel(billingService, orderService, customerService);

        // 1. Initial AmountPaid should be 0
        Assert.Equal(0m, vm.AmountPaid);

        // 2. Type "5", "0", "0" -> 500
        vm.NumpadDigitCommand.Execute("5");
        Assert.Equal(5m, vm.AmountPaid);
        vm.NumpadDigitCommand.Execute("0");
        Assert.Equal(50m, vm.AmountPaid);
        vm.NumpadDigitCommand.Execute("0");
        Assert.Equal(500m, vm.AmountPaid);

        // 3. Type "000" -> 500,000
        vm.NumpadTripleZeroCommand.Execute(null);
        Assert.Equal(500000m, vm.AmountPaid);

        // 4. Backspace -> 50,000
        vm.NumpadBackspaceCommand.Execute(null);
        Assert.Equal(50000m, vm.AmountPaid);

        // 5. Clear -> 0
        vm.NumpadClearCommand.Execute(null);
        Assert.Equal(0m, vm.AmountPaid);
    }

    [Fact]
    public void BillingViewModel_TouchNumpad_QuickCash_And_ExactAmount()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var billingService = new BillingService(context);
        var orderService = new OrderService(context);
        var customerService = new CustomerService(context);

        var vm = new BillingViewModel(billingService, orderService, customerService);
        vm.SubTotal = 150000m;
        // TotalDue with 8% VAT = 150000 * 1.08 = 162,000 VND
        Assert.Equal(162000m, vm.TotalDue);

        // Quick cash +100k
        vm.QuickCashCommand.Execute("100000");
        Assert.Equal(100000m, vm.AmountPaid);
        Assert.Equal(0m, vm.ChangeDue);

        // Quick cash +100k -> 200,000
        vm.QuickCashCommand.Execute("100000");
        Assert.Equal(200000m, vm.AmountPaid);
        Assert.Equal(38000m, vm.ChangeDue); // 200,000 - 162,000 = 38,000

        // Exact amount button
        vm.ExactAmountCommand.Execute(null);
        Assert.Equal(162000m, vm.AmountPaid);
        Assert.Equal(0m, vm.ChangeDue);
    }

    [Fact]
    public void BillingViewModel_TouchNumpad_CapsAt100Million_ToPreventOverflow()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var billingService = new BillingService(context);
        var orderService = new OrderService(context);
        var customerService = new CustomerService(context);

        var vm = new BillingViewModel(billingService, orderService, customerService);
        vm.AmountPaid = 90000000m; // 90M

        // Appending 0 would make it 900M (> 100M cap), should be rejected
        vm.NumpadDigitCommand.Execute("0");
        Assert.Equal(90000000m, vm.AmountPaid);

        // Triple zero would make it 90B (> 100M cap), should be rejected
        vm.NumpadTripleZeroCommand.Execute(null);
        Assert.Equal(90000000m, vm.AmountPaid);
    }

    [Fact]
    public void KitchenKdsViewModel_ToggleChime_SwitchesStateAndText()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var kdsService = new KdsService(context);
        var orderService = new OrderService(context);

        var vm = new KitchenKdsViewModel(kdsService, orderService);

        // Initial default is enabled
        Assert.True(vm.IsChimeEnabled);
        Assert.Equal("Chuông: BẬT", vm.ChimeButtonText);

        // Toggle to disabled
        vm.ToggleChimeCommand.Execute(null);
        Assert.False(vm.IsChimeEnabled);
        Assert.Equal("Chuông: TẮT", vm.ChimeButtonText);

        // Toggle back to enabled
        vm.ToggleChimeCommand.Execute(null);
        Assert.True(vm.IsChimeEnabled);
        Assert.Equal("Chuông: BẬT", vm.ChimeButtonText);
    }

    [Fact]
    public void PosOrderViewModel_RemoveCartItem_ShowsToast_AndUndoRestoresItem()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var menuService = new MenuService(context);
        var orderService = new OrderService(context);
        var inventoryService = new InventoryService(context);

        var vm = new PosOrderViewModel(menuService, orderService, inventoryService);

        var item1 = new CartItemViewModel { MenuItemId = 1, DishName = "Phở Bò Tái", UnitPrice = 60000m, Quantity = 2 };
        var item2 = new CartItemViewModel { MenuItemId = 2, DishName = "Trà Đào Cam Sả", UnitPrice = 35000m, Quantity = 1 };
        vm.CartItems.Add(item1);
        vm.CartItems.Add(item2);
        vm.SubTotal = 155000m;

        // Remove item1
        vm.RemoveFromCartCommand.Execute(item1);

        Assert.Single(vm.CartItems);
        Assert.Equal(35000m, vm.SubTotal);
        Assert.True(vm.IsToastVisible);
        Assert.Contains("Phở Bò Tái", vm.ToastMessage);

        // Execute Undo Toast
        vm.UndoToastCommand.Execute(null);

        Assert.Equal(2, vm.CartItems.Count);
        Assert.Equal(155000m, vm.SubTotal);
        Assert.False(vm.IsToastVisible);
        Assert.Equal(item1.MenuItemId, vm.CartItems[0].MenuItemId);
    }

    [Fact]
    public void PosOrderViewModel_ClearCart_ShowsToast_AndUndoRestoresCart()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var menuService = new MenuService(context);
        var orderService = new OrderService(context);
        var inventoryService = new InventoryService(context);

        var vm = new PosOrderViewModel(menuService, orderService, inventoryService);

        var item1 = new CartItemViewModel { MenuItemId = 1, DishName = "Cơm Gà Xối Mỡ", UnitPrice = 55000m, Quantity = 1 };
        var item2 = new CartItemViewModel { MenuItemId = 2, DishName = "Nước Mía", UnitPrice = 15000m, Quantity = 2 };
        vm.CartItems.Add(item1);
        vm.CartItems.Add(item2);
        vm.SubTotal = 85000m;

        // User clears cart
        vm.ClearCartCommand.Execute(null);

        Assert.Empty(vm.CartItems);
        Assert.Equal(0m, vm.SubTotal);
        Assert.True(vm.IsToastVisible);
        Assert.Contains("toàn bộ", vm.ToastMessage);

        // Undo
        vm.UndoToastCommand.Execute(null);

        Assert.Equal(2, vm.CartItems.Count);
        Assert.Equal(85000m, vm.SubTotal);
        Assert.False(vm.IsToastVisible);
    }

    [Fact]
    public async Task FloorPlanViewModel_CleanTableDone_ShowsToast_AndUndoRestoresCleaningStatus()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var tableService = new TableService(context);
        var orderService = new OrderService(context);

        var area = new DiningArea { AreaName = "Sảnh VIP" };
        context.DiningAreas.Add(area);
        await context.SaveChangesAsync();

        var table = new DiningTable
        {
            TableName = "VIP-09",
            Capacity = 6,
            Status = TableStatus.Cleaning,
            AreaId = area.AreaId
        };
        context.DiningTables.Add(table);
        await context.SaveChangesAsync();

        var vm = new FloorPlanViewModel(tableService);
        await vm.LoadTablesAsync();

        var tableInVm = vm.Tables.First(t => t.TableId == table.TableId);

        // Mark table as clean done: Cleaning -> Available
        await vm.CleanTableDoneCommand.ExecuteAsync(tableInVm);

        Assert.Equal(TableStatus.Available, tableInVm.Status);
        Assert.True(vm.IsToastVisible);
        Assert.Contains("VIP-09", vm.ToastMessage);

        // Execute Undo Toast: Available -> Cleaning
        await vm.UndoToastCommand.ExecuteAsync(null);

        Assert.Equal(TableStatus.Cleaning, tableInVm.Status);
        Assert.False(vm.IsToastVisible);
    }
}
