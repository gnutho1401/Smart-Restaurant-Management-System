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
    private readonly IAuditService? _auditService;

    public TableService(RestaurantDbContext context, IAuditService? auditService = null)
    {
        _context = context;
        _auditService = auditService;
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
            .Include(t => t.Orders)
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

    public async Task<bool> RevertTableStatusAsync(int tableId, TableStatus previousStatus)
    {
        var table = await _context.DiningTables.FindAsync(tableId);
        if (table == null) return false;

        table.Status = previousStatus;

        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                $"Đã xảy ra xung đột khi hoàn tác trạng thái bàn {table.TableName}. Dữ liệu đã bị thay đổi bởi người khác.", ex);
        }
    }

    public async Task<bool> TransferTableAsync(int sourceTableId, int targetTableId)
    {
        if (sourceTableId == targetTableId) return false;

        var sourceTable = await _context.DiningTables
            .Include(t => t.Orders)
            .FirstOrDefaultAsync(t => t.TableId == sourceTableId);

        var targetTable = await _context.DiningTables.FirstOrDefaultAsync(t => t.TableId == targetTableId);

        if (sourceTable == null || targetTable == null)
        {
            throw new KeyNotFoundException("Không tìm thấy bàn nguồn hoặc bàn đích.");
        }

        if (sourceTable.Status != TableStatus.Occupied)
        {
            throw new InvalidOperationException($"Bàn nguồn '{sourceTable.TableName}' không có khách để chuyển.");
        }

        if (targetTable.Status != TableStatus.Available)
        {
            throw new InvalidOperationException($"Bàn đích '{targetTable.TableName}' đang bận hoặc chưa dọn dẹp, không thể chuyển sang.");
        }

        var activeOrder = sourceTable.Orders.FirstOrDefault(o => o.Status == "Active");
        if (activeOrder != null)
        {
            activeOrder.TableId = targetTableId;
        }

        targetTable.Status = TableStatus.Occupied;
        sourceTable.Status = TableStatus.Available;

        await _context.SaveChangesAsync();

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                null, "Staff", "Staff", "TransferTable", "DiningTable", targetTableId.ToString(),
                sourceTable.TableName, targetTable.TableName,
                $"Chuyển toàn bộ đơn hàng từ bàn {sourceTable.TableName} sang bàn {targetTable.TableName}");
        }

        return true;
    }

    public async Task<bool> MergeTablesAsync(int primaryTableId, int secondaryTableId)
    {
        if (primaryTableId == secondaryTableId) return false;

        var primaryTable = await _context.DiningTables
            .Include(t => t.Orders)
                .ThenInclude(o => o.OrderItems)
            .FirstOrDefaultAsync(t => t.TableId == primaryTableId);

        var secondaryTable = await _context.DiningTables
            .Include(t => t.Orders)
                .ThenInclude(o => o.OrderItems)
            .FirstOrDefaultAsync(t => t.TableId == secondaryTableId);

        if (primaryTable == null || secondaryTable == null)
        {
            throw new KeyNotFoundException("Không tìm thấy bàn chính hoặc bàn phụ.");
        }

        var primaryOrder = primaryTable.Orders.FirstOrDefault(o => o.Status == "Active");
        var secondaryOrder = secondaryTable.Orders.FirstOrDefault(o => o.Status == "Active");

        if (primaryOrder == null || secondaryOrder == null)
        {
            throw new InvalidOperationException("Cả hai bàn đều phải có đơn hàng đang hoạt động để gộp bàn.");
        }

        // Move all items from secondary order into primary order
        foreach (var item in secondaryOrder.OrderItems.ToList())
        {
            item.OrderId = primaryOrder.OrderId;
            primaryOrder.OrderItems.Add(item);
        }

        primaryOrder.SubTotal = primaryOrder.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity);
        primaryOrder.FinalTotal = Math.Max(0, primaryOrder.SubTotal - primaryOrder.DiscountAmount);

        // Mark secondary order merged
        secondaryOrder.Status = "Completed";
        secondaryTable.Status = TableStatus.Available;

        await _context.SaveChangesAsync();

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                null, "Staff", "Staff", "MergeTable", "DiningTable", primaryTableId.ToString(),
                secondaryTable.TableName, primaryTable.TableName,
                $"Gộp bàn {secondaryTable.TableName} vào bàn {primaryTable.TableName} (Gộp {secondaryOrder.OrderItems.Count} món)");
        }

        return true;
    }
}
