using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Exceptions;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;

namespace SmartRestaurant.Services;

/// <summary>
/// Service executing atomic Bill-of-Materials (BOM) inventory deduction and optimistic concurrency control.
/// Invariant: Stock_new(R_k) = Stock_old(R_k) - Sum(Q_i * BOM(D_i, R_k)) >= 0 for all ingredients.
/// Any violation aborts the entire transaction via atomic rollback.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly RestaurantDbContext _context;

    public InventoryService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<bool> DeductBomIngredientsAsync(int orderId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                throw new KeyNotFoundException($"Order ID {orderId} không tồn tại.");
            }

            var menuItemIds = order.OrderItems.Select(oi => oi.MenuItemId).Distinct().ToList();
            var recipes = await _context.Recipes
                .Where(r => menuItemIds.Contains(r.MenuItemId))
                .ToListAsync();

            // 1. Calculate Aggregate BOM Requirements per Raw Ingredient
            var requiredQuantities = new Dictionary<int, decimal>();

            foreach (var item in order.OrderItems)
            {
                var dishRecipes = recipes.Where(r => r.MenuItemId == item.MenuItemId);
                foreach (var recipe in dishRecipes)
                {
                    decimal totalNeeded = recipe.QuantityRequired * item.Quantity;
                    if (requiredQuantities.TryGetValue(recipe.IngredientId, out decimal currentNeeded))
                    {
                        requiredQuantities[recipe.IngredientId] = currentNeeded + totalNeeded;
                    }
                    else
                    {
                        requiredQuantities[recipe.IngredientId] = totalNeeded;
                    }
                }
            }

            var ingredientIds = requiredQuantities.Keys.ToList();
            var ingredients = await _context.Ingredients
                .Where(ing => ingredientIds.Contains(ing.IngredientId))
                .ToListAsync();

            // 2. Mathematical Invariant Verification: All Stock >= Required
            foreach (var (ingredientId, requiredQty) in requiredQuantities)
            {
                var ingredient = ingredients.FirstOrDefault(i => i.IngredientId == ingredientId);
                if (ingredient == null)
                {
                    throw new KeyNotFoundException($"Không tìm thấy nguyên liệu kho ID {ingredientId}.");
                }

                if (ingredient.CurrentStock < requiredQty)
                {
                    await transaction.RollbackAsync();
                    throw new InsufficientStockException(
                        ingredient.IngredientId,
                        ingredient.Name,
                        requiredQty,
                        ingredient.CurrentStock);
                }
            }

            // 3. Atomic Stock Deduction
            foreach (var (ingredientId, requiredQty) in requiredQuantities)
            {
                var ingredient = ingredients.First(i => i.IngredientId == ingredientId);
                ingredient.CurrentStock -= requiredQty;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync();
            throw new ConcurrencyConflictException(
                "Kho nguyên liệu vừa được cập nhật bởi quầy khác. Giao dịch đã được hủy an toàn, vui lòng thử lại.", ex);
        }
    }

    public async Task<IEnumerable<Ingredient>> GetAllIngredientsAsync()
    {
        return await _context.Ingredients
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Ingredient>> GetLowStockAlertsAsync()
    {
        return await _context.Ingredients
            .Where(i => i.CurrentStock <= i.MinStock)
            .OrderBy(i => i.CurrentStock)
            .ToListAsync();
    }

    public async Task<bool> RestockIngredientAsync(int ingredientId, decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Số lượng nhập kho phải lớn hơn 0.");
        }

        var ingredient = await _context.Ingredients.FirstOrDefaultAsync(i => i.IngredientId == ingredientId);
        if (ingredient == null) return false;

        ingredient.CurrentStock += quantity;
        await _context.SaveChangesAsync();
        return true;
    }
}
