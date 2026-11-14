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
/// Service powering the Kitchen Display System (KDS) order queue and multi-station ticket dispatching.
/// </summary>
public class KdsService : IKdsService
{
    private readonly RestaurantDbContext _context;

    public KdsService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<OrderItem>> GetActiveKitchenTicketsAsync()
    {
        try
        {
            return await _context.OrderItems
                .AsNoTracking()
                .Include(oi => oi.MenuItem)
                .Include(oi => oi.Order)
                    .ThenInclude(o => o!.Table)
                .Where(oi => oi.CookingStatus != CookingStatus.Served && oi.Order!.Status == "Active")
                .OrderBy(oi => oi.Order!.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("Invalid column name") || ex.Message.Contains("CustomerId") || ex.Message.Contains("IsCancelled"))
        {
            await DbInitializer.EnsureSchemaUpgradesAsync(_context);
            try
            {
                return await _context.OrderItems
                    .AsNoTracking()
                    .Include(oi => oi.MenuItem)
                    .Include(oi => oi.Order)
                        .ThenInclude(o => o!.Table)
                    .Where(oi => oi.CookingStatus != CookingStatus.Served && oi.Order!.Status == "Active")
                    .OrderBy(oi => oi.Order!.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception innerEx)
            {
                System.Diagnostics.Debug.WriteLine($"[KdsService] Fallback ticket load notice: {innerEx.Message}");
                return new List<OrderItem>();
            }
        }
    }

    public async Task<bool> UpdateTicketStatusAsync(int orderItemId, CookingStatus nextStatus)
    {
        var item = await _context.OrderItems.FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId);
        if (item == null)
        {
            throw new KeyNotFoundException($"Kitchen ticket item ID {orderItemId} not found.");
        }

        item.CookingStatus = nextStatus;
        await _context.SaveChangesAsync();
        return true;
    }
}
