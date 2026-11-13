using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;

namespace SmartRestaurant.Services;

/// <summary>
/// Service managing POS order lifecycle, live subtotal calculations, and culinary allergy notes.
/// </summary>
public class OrderService : IOrderService
{
    private readonly RestaurantDbContext _context;

    public OrderService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<Order> CreateOrderAsync(int tableId, int userId, List<(int menuItemId, int quantity, string? note)> items)
    {
        if (items == null || !items.Any())
        {
            throw new ArgumentException("Đơn hàng phải chứa ít nhất một món ăn.", nameof(items));
        }

        var table = await _context.DiningTables.FirstOrDefaultAsync(t => t.TableId == tableId);
        if (table == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy bàn ăn ID {tableId}.");
        }

        // Auto-seat table if currently Available
        if (table.Status == TableStatus.Available)
        {
            table.Status = TableStatus.Occupied;
        }

        // Check if there is already an active order on this table
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.TableId == tableId && o.Status == "Active");

        if (order == null)
        {
            order = new Order
            {
                TableId = tableId,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                Status = "Active",
                SubTotal = 0,
                DiscountAmount = 0,
                FinalTotal = 0
            };
            await _context.Orders.AddAsync(order);
            await _context.SaveChangesAsync();
        }

        var requestedItemIds = items.Select(i => i.menuItemId).Distinct().ToList();
        var menuItems = await _context.MenuItems
            .Where(m => requestedItemIds.Contains(m.MenuItemId))
            .ToDictionaryAsync(m => m.MenuItemId);

        foreach (var (menuItemId, quantity, note) in items)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(items), $"Số lượng món phải lớn hơn 0 (Món ID {menuItemId}).");
            }

            if (!menuItems.TryGetValue(menuItemId, out var dish))
            {
                throw new KeyNotFoundException($"Không tìm thấy món ăn ID {menuItemId}.");
            }

            var orderItem = new OrderItem
            {
                OrderId = order.OrderId,
                MenuItemId = dish.MenuItemId,
                Quantity = quantity,
                UnitPrice = dish.Price, // Historical price locked
                CookingStatus = CookingStatus.Pending,
                Note = note
            };

            order.OrderItems.Add(orderItem);
        }

        // Recalculate SubTotal ignoring cancelled items
        order.SubTotal = order.OrderItems.Where(i => !i.IsCancelled).Sum(i => i.UnitPrice * i.Quantity);
        order.FinalTotal = Math.Max(0, order.SubTotal - order.DiscountAmount);

        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> GetActiveOrderByTableIdAsync(int tableId)
    {
        return await _context.Orders
            .Include(o => o.Table)
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
            .FirstOrDefaultAsync(o => o.TableId == tableId && o.Status == "Active");
    }

    public async Task<Order?> GetOrderByIdAsync(int orderId)
    {
        return await _context.Orders
            .Include(o => o.Table)
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
            .Include(o => o.Invoice!)
                .ThenInclude(inv => inv.Payments)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);
    }

    public async Task<bool> UpdateItemCookingStatusAsync(int orderItemId, CookingStatus newStatus)
    {
        var item = await _context.OrderItems.FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId);
        if (item == null) return false;

        item.CookingStatus = newStatus;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> VoidOrderItemAsync(int orderItemId, string reason, int actorUserId)
    {
        var item = await _context.OrderItems
            .Include(oi => oi.MenuItem)
                .ThenInclude(m => m!.Recipes)
                    .ThenInclude(r => r.Ingredient)
            .FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId);

        if (item == null || item.IsCancelled)
        {
            return false;
        }

        // Smart BOM return: if kitchen hasn't started yet (Pending), return raw ingredients to inventory
        if (item.CookingStatus == CookingStatus.Pending && item.MenuItem?.Recipes != null)
        {
            foreach (var recipe in item.MenuItem.Recipes)
            {
                if (recipe.Ingredient != null)
                {
                    recipe.Ingredient.CurrentStock += (recipe.QuantityRequired * item.Quantity);
                }
            }
        }
        // If Cooking or Ready: ingredients are already wasted/consumed in the kitchen, not returned.

        item.IsCancelled = true;
        item.CancelReason = string.IsNullOrWhiteSpace(reason) ? "Khách yêu cầu hủy món" : reason.Trim();
        item.CancelledAt = DateTime.UtcNow;

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderId == item.OrderId);

        if (order != null)
        {
            order.SubTotal = order.OrderItems.Where(i => !i.IsCancelled).Sum(i => i.UnitPrice * i.Quantity);
            order.FinalTotal = Math.Max(0, order.SubTotal - order.DiscountAmount);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AttachCustomerToOrderAsync(int orderId, int customerId)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (order == null || customer == null)
        {
            return false;
        }

        order.CustomerId = customerId;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ApplyMemberDiscountAsync(int orderId, int customerId, int pointsToUse)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (order == null || customer == null)
        {
            return false;
        }

        order.CustomerId = customerId;
        decimal subTotal = order.OrderItems.Where(i => !i.IsCancelled).Sum(i => i.UnitPrice * i.Quantity);

        // Tier discount percentage
        decimal tierDiscountRate = customer.Tier switch
        {
            MembershipTier.Silver => 0.05m,
            MembershipTier.Gold => 0.10m,
            MembershipTier.Diamond => 0.15m,
            _ => 0m
        };

        decimal tierDiscountAmount = Math.Round(subTotal * tierDiscountRate, 0);

        // Loyalty points discount (1 point = 1,000 VND)
        decimal pointsDiscount = 0;
        if (pointsToUse > 0)
        {
            int usablePoints = Math.Min(pointsToUse, customer.LoyaltyPoints);
            pointsDiscount = usablePoints * 1000m;
        }

        decimal totalDiscount = tierDiscountAmount + pointsDiscount;
        if (totalDiscount > subTotal)
        {
            totalDiscount = subTotal;
        }

        order.DiscountAmount = totalDiscount;
        order.FinalTotal = Math.Max(0, subTotal - totalDiscount);

        await _context.SaveChangesAsync();
        return true;
    }
}
