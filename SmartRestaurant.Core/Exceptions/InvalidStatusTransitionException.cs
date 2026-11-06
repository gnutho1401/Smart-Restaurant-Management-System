using System;

namespace SmartRestaurant.Core.Exceptions;

/// <summary>
/// Thrown when an illegal state machine transition is attempted.
/// </summary>
public class InvalidStatusTransitionException : InvalidOperationException
{
    public string FromStatus { get; }
    public string ToStatus { get; }

    public InvalidStatusTransitionException(string fromStatus, string toStatus, string entityName = "Entity")
        : base($"Invalid status transition for {entityName}: from '{fromStatus}' to '{toStatus}'.")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}
