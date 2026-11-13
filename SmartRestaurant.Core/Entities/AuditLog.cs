using System;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Immutable audit log trail entity recording all sensitive restaurant transactions and state mutations.
/// Adheres to append-only compliance standards.
/// </summary>
public class AuditLog
{
    public int AuditLogId { get; set; }
    public int? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // e.g. "VoidOrderItem", "ApplyDiscount", "TransferTable", "MergeTable", "OpenShift", "CloseShift", "CreateStaff", "UpdateStaffRole", "DeleteStaff"
    public string EntityName { get; set; } = string.Empty; // e.g. "Order", "OrderItem", "DiningTable", "WorkShift", "User", "Invoice"
    public string? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Details { get; set; }
    public string? MachineName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

