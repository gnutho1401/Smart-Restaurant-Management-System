namespace SmartRestaurant.Core.Enums;

/// <summary>
/// Operational states for dining tables conforming to the DFA state machine.
/// </summary>
public enum TableStatus
{
    Available,
    Occupied,
    Reserved,
    Cleaning
}
