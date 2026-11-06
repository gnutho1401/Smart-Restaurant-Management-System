using System;

namespace SmartRestaurant.Core.Exceptions;

/// <summary>
/// Thrown when raw material stock balance is insufficient to satisfy BOM requirements.
/// </summary>
public class InsufficientStockException : InvalidOperationException
{
    public int IngredientId { get; }
    public string IngredientName { get; }
    public decimal RequiredQuantity { get; }
    public decimal AvailableStock { get; }

    public InsufficientStockException(int ingredientId, string ingredientName, decimal required, decimal available)
        : base($"Insufficient stock for '{ingredientName}' (ID: {ingredientId}). Required: {required}, Available: {available}.")
    {
        IngredientId = ingredientId;
        IngredientName = ingredientName;
        RequiredQuantity = required;
        AvailableStock = available;
    }
}
