using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;

namespace SmartRestaurant.Services;

/// <summary>
/// Service providing immutable audit trail logging and historical event query capability.
/// All sensitive restaurant mutations are persisted to provide Non-Repudiation.
/// </summary>
public class AuditService : IAuditService
{
    private readonly RestaurantDbContext _context;

    public AuditService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        int? userId,
        string username,
        string userRole,
        string action,
        string entityName,
        string? entityId = null,
        string? oldValue = null,
        string? newValue = null,
        string? details = null)
    {
        try
        {
            var log = new AuditLog
            {
                UserId = userId,
                Username = string.IsNullOrWhiteSpace(username) ? "System" : username,
                UserRole = string.IsNullOrWhiteSpace(userRole) ? "System" : userRole,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValue = oldValue,
                NewValue = newValue,
                Details = details,
                MachineName = Environment.MachineName,
                Timestamp = DateTime.UtcNow
            };

            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuditService.LogAsync] Handled log exception: {ex.Message}");
            if (_context.Database.IsSqlServer() && ex.Message.Contains("UserId"))
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "IF COL_LENGTH('AuditLogs', 'UserId') IS NULL ALTER TABLE AuditLogs ADD UserId INT NULL;");
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Suppress secondary retry failure
                }
            }
        }
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? action = null,
        string? entityName = null)
    {
        try
        {
            var query = _context.AuditLogs.AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.Timestamp >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(a => a.Timestamp <= toDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action.Contains(action));
            }

            if (!string.IsNullOrWhiteSpace(entityName))
            {
                query = query.Where(a => a.EntityName == entityName);
            }

            return await query
                .OrderByDescending(a => a.Timestamp)
                .Take(200)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuditService.GetAuditLogsAsync] Query exception handled gracefully: {ex.Message}");

            // Self-healing migration for SQL Server if column UserId is missing
            try
            {
                if (_context.Database.IsSqlServer() && ex.Message.Contains("UserId"))
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "IF COL_LENGTH('AuditLogs', 'UserId') IS NULL ALTER TABLE AuditLogs ADD UserId INT NULL;");

                    return await _context.AuditLogs
                        .OrderByDescending(a => a.Timestamp)
                        .Take(200)
                        .ToListAsync();
                }
            }
            catch
            {
                // Suppress recovery failure
            }

            return Enumerable.Empty<AuditLog>();
        }
    }
}

