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

    public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? action = null,
        string? entityName = null)
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
}

