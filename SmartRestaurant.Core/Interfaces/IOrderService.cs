using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Service contract for touch POS ordering workflows and allergy tracking.
/// </summary>
public interface IOrderService
{
    Task<Order> CreateOrderAsync(int tableId, int userId, List<(int menuItemId, int quantity, string? note)> items);
    Task<Order?> GetActiveOrderByTableIdAsync(int tableId);
    Task<Order?> GetOrderByIdAsync(int orderId);
    Task<bool> UpdateItemCookingStatusAsync(int orderItemId, CookingStatus newStatus);
}
