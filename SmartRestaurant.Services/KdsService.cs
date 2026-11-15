using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly RestaurantDbContext? _context;
    private readonly IDbContextFactory<RestaurantDbContext>? _contextFactory;

    public KdsService(RestaurantDbContext context)
    {
        _context = context;
    }

    [ActivatorUtilitiesConstructor]
    public KdsService(IDbContextFactory<RestaurantDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IEnumerable<OrderItem>> GetActiveKitchenTicketsAsync()
    {
        if (_contextFactory != null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await QueryTicketsInternalAsync(context);
        }
        return await QueryTicketsInternalAsync(_context!);
    }

    private static async Task<IEnumerable<OrderItem>> QueryTicketsInternalAsync(RestaurantDbContext context)
    {
        try
        {
            return await context.OrderItems
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
            await DbInitializer.EnsureSchemaUpgradesAsync(context);
            try
            {
                return await context.OrderItems
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
        if (_contextFactory != null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await UpdateTicketStatusInternalAsync(context, orderItemId, nextStatus);
        }
        return await UpdateTicketStatusInternalAsync(_context!, orderItemId, nextStatus);
    }

    private static async Task<bool> UpdateTicketStatusInternalAsync(RestaurantDbContext context, int orderItemId, CookingStatus nextStatus)
    {
        var item = await context.OrderItems.FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId);
        if (item == null)
        {
            throw new KeyNotFoundException($"Kitchen ticket item ID {orderItemId} not found.");
        }

        item.CookingStatus = nextStatus;
        await context.SaveChangesAsync();
        return true;
    }
}
