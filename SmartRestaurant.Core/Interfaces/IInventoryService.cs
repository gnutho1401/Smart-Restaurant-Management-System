using System.Collections.Generic;
using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Service contract for atomic BOM inventory deduction and low-stock alerts.
/// </summary>
public interface IInventoryService
{
    Task<bool> DeductBomIngredientsAsync(int orderId);
    Task<IEnumerable<Ingredient>> GetAllIngredientsAsync();
    Task<IEnumerable<Ingredient>> GetLowStockAlertsAsync();
    Task<bool> RestockIngredientAsync(int ingredientId, decimal quantity);
    Task<int> ApplyPurchaseOrderDraftAsync(IEnumerable<PurchaseOrderDraftItem> poItems);
}
