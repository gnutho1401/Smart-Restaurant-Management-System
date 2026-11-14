namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Data transfer record representing a line item in an automated AI-generated Purchase Order (PO).
/// </summary>
public record PurchaseOrderDraftItem(
    int IngredientId,
    string IngredientName,
    decimal QuantityToOrder,
    string Unit,
    string Urgency,
    string Reason);

