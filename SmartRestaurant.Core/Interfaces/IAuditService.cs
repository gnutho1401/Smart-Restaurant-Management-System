using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Service contract for append-only business and security audit trail logging.
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        int? userId,
        string username,
        string userRole,
        string action,
        string entityName,
        string? entityId = null,
        string? oldValue = null,
        string? newValue = null,
        string? details = null);

    Task<IEnumerable<AuditLog>> GetAuditLogsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? action = null,
        string? entityName = null);
}

