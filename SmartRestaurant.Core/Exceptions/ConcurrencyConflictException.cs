using System;

namespace SmartRestaurant.Core.Exceptions;

/// <summary>
/// Thrown when an optimistic concurrency conflict is detected during concurrent updates.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
