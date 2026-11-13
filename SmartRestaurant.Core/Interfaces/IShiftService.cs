using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Service contract for cashier shift lifecycle, opening float declaration, and end-of-shift Z-Report balancing.
/// </summary>
public interface IShiftService
{
    Task<WorkShift> OpenShiftAsync(int userId, decimal openingFloat);
    Task<WorkShift?> GetCurrentOpenShiftAsync(int userId);
    Task<WorkShift> CloseShiftAsync(int shiftId, decimal actualCashEnd, string? note = null);
    Task<IEnumerable<WorkShift>> GetShiftHistoryAsync();
}

