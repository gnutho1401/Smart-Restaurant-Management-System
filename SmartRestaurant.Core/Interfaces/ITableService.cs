using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Service contract for dining table floor plan management and DFA transitions.
/// </summary>
public interface ITableService
{
    Task<IEnumerable<DiningTable>> GetAllTablesAsync();
    Task<DiningTable?> GetTableByIdAsync(int tableId);
    Task<bool> TransitionTableStatusAsync(int tableId, TableStatus newStatus);
    Task<bool> TransferTableAsync(int sourceTableId, int targetTableId);
    Task<bool> MergeTablesAsync(int primaryTableId, int secondaryTableId);
}
