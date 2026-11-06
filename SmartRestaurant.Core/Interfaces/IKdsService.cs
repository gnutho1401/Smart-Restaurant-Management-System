using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Service contract for Kitchen Display System (KDS) live ticket queue.
/// </summary>
public interface IKdsService
{
    Task<IEnumerable<OrderItem>> GetActiveKitchenTicketsAsync();
    Task<bool> UpdateTicketStatusAsync(int orderItemId, CookingStatus nextStatus);
}
