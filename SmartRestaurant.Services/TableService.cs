using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Exceptions;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;

namespace SmartRestaurant.Services;

/// <summary>
/// Service managing front-of-house dining table life cycle governed by a Deterministic Finite Automaton (DFA).
/// Invariant: Direct transitions from Occupied to Available without passing through Cleaning are strictly prohibited.
/// </summary>
public class TableService : ITableService
{
    private readonly RestaurantDbContext _context;

    public TableService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<DiningTable>> GetAllTablesAsync()
    {
        return await _context.DiningTables
            .Include(t => t.Area)
            .OrderBy(t => t.TableId)
            .ToListAsync();
    }

    public async Task<DiningTable?> GetTableByIdAsync(int tableId)
    {
        return await _context.DiningTables
            .Include(t => t.Area)
            .Include(t => t.Orders.Where(o => o.Status == "Active"))
            .FirstOrDefaultAsync(t => t.TableId == tableId);
    }

    public async Task<bool> TransitionTableStatusAsync(int tableId, TableStatus newStatus)
    {
        var table = await _context.DiningTables.FirstOrDefaultAsync(t => t.TableId == tableId);
        if (table == null)
        {
            throw new KeyNotFoundException($"Dining table ID {tableId} not found.");
        }

        if (table.Status == newStatus)
        {
            return true; // No-op idempotent transition
        }

        // DFA Table State Machine Invariant Verification
        bool isValid = (table.Status, newStatus) switch
        {
            (TableStatus.Available, TableStatus.Occupied) => true,
            (TableStatus.Available, TableStatus.Reserved) => true,
            (TableStatus.Reserved, TableStatus.Occupied) => true,
            (TableStatus.Reserved, TableStatus.Available) => true,
            (TableStatus.Occupied, TableStatus.Cleaning) => true,
            (TableStatus.Cleaning, TableStatus.Available) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidStatusTransitionException(
                table.Status.ToString(),
                newStatus.ToString(),
                $"DiningTable '{table.TableName}' (ID: {tableId})");
        }

        table.Status = newStatus;

        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                $"Bàn {table.TableName} vừa được cập nhật bởi một quầy khác. Vui lòng tải lại dữ liệu.", ex);
        }
    }
}
