using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.AIEngine.Models;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;

namespace SmartRestaurant.AIEngine;

/// <summary>
/// Computational analytics engine combining deterministic statistical algorithms (BCG Matrix, Peak Volume)
/// with generative natural-language executive advisory.
/// </summary>
public class AiAnalyticsEngine
{
    private readonly RestaurantDbContext _context;
    private readonly IAiService _aiService;

    public AiAnalyticsEngine(RestaurantDbContext context, IAiService aiService)
    {
        _context = context;
        _aiService = aiService;
    }

    public async Task<BcgMatrixResult> ComputeBcgMatrixAsync()
    {
        var dishes = await _context.MenuItems
            .Include(m => m.Category)
            .ToListAsync();

        var sales = await _context.OrderItems
            .GroupBy(oi => oi.MenuItemId)
            .Select(g => new { MenuItemId = g.Key, TotalSold = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.UnitPrice * x.Quantity) })
            .ToListAsync();

        var salesDict = sales.ToDictionary(s => s.MenuItemId);

        decimal medianSold = sales.Any() ? (decimal)sales.Average(s => s.TotalSold) : 1;
        decimal medianPrice = dishes.Any() ? dishes.Average(d => d.Price) : 100000;

        var items = new List<BcgItem>();
        foreach (var dish in dishes)
        {
            int sold = salesDict.TryGetValue(dish.MenuItemId, out var s) ? s.TotalSold : 0;
            decimal rev = salesDict.TryGetValue(dish.MenuItemId, out var s2) ? s2.Revenue : 0;

            bool highVolume = sold >= medianSold;
            bool highMargin = dish.Price >= medianPrice;

            string classification;
            string action;

            if (highVolume && highMargin)
            {
                classification = "Star (Ngôi Sao)";
                action = "Món sinh lời cao, bán chạy: Duy trì chất lượng tuyệt đối và định vị vị trí đắc địa trên thực đơn.";
            }
            else if (highVolume && !highMargin)
            {
                classification = "Plowhorse (Bò Sữa)";
                action = "Bán rất chạy nhưng lợi nhuận biên thấp: Tăng nhẹ giá bán 5-8% hoặc tối ưu công thức BOM để giảm chi phí nguyên liệu.";
            }
            else if (!highVolume && highMargin)
            {
                classification = "Puzzle (Dấu Chấm Hỏi)";
                action = "Lợi nhuận biên cao nhưng ít người gọi: Đẩy mạnh quảng bá, đào tạo nhân viên phục vụ gợi ý combo (Upselling).";
            }
            else
            {
                classification = "Dog (Chó Mực)";
                action = "Bán chậm, lợi nhuận thấp: Cân nhắc loại bỏ khỏi thực đơn để tinh gọn kho nguyên liệu.";
            }

            items.Add(new BcgItem(dish.Name, dish.Category?.CategoryName ?? "Chung", sold, rev, classification, action));
        }

        string narrative = await _aiService.GenerateMenuInsightsAsync();
        return new BcgMatrixResult(items, narrative, DateTime.UtcNow);
    }

    public async Task<PeakHourResult> ComputePeakHoursAsync()
    {
        var orders = await _context.Orders.ToListAsync();

        var slots = new List<PeakHourSlot>();
        for (int h = 9; h <= 22; h++)
        {
            int count = orders.Count(o => o.CreatedAt.Hour == h);
            string level = count switch
            {
                >= 8 => "Cao Điểm (Rất Đông)",
                >= 4 => "Trung Bình",
                _ => "Thấp Điểm (Vắng)"
            };
            slots.Add(new PeakHourSlot($"{h:D2}:00 - {h + 1:D2}:00", count, level));
        }

        string advice = await _aiService.AnalyzePeakHoursAsync();
        return new PeakHourResult(slots, "Trưa 11:30 - 13:30 & Tối 18:00 - 20:30", advice);
    }

    public async Task<RevenueInsightResult> ComputeRevenueInsightsAsync(DateTime startDate, DateTime endDate)
    {
        var orders = await _context.Orders
            .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate && o.Status == "Completed")
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
            .ToListAsync();

        decimal totalRevenue = orders.Sum(o => o.FinalTotal);
        int totalOrders = orders.Count;

        var topDishes = orders
            .SelectMany(o => o.OrderItems)
            .GroupBy(oi => oi.MenuItem != null ? oi.MenuItem.Name : "Khác")
            .Select(g => new { Dish = g.Key, Portions = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.UnitPrice * x.Quantity) })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .Select(x => $"{x.Dish} ({x.Portions} phần - {x.Revenue:N0} VND)")
            .ToList();

        string aiAnalysis = await _aiService.AnalyzeRevenueTrendsAsync(startDate, endDate);

        return new RevenueInsightResult(
            $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}",
            totalRevenue,
            totalOrders,
            topDishes,
            aiAnalysis);
    }

    public async Task<WeeklyReorderResult> ComputeWeeklyReorderForecastAsync()
    {
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var recentOrderItems = await _context.OrderItems
            .Where(oi => oi.Order != null && oi.Order.CreatedAt >= sevenDaysAgo && oi.Order.Status == "Completed")
            .Include(oi => oi.MenuItem)
                .ThenInclude(m => m!.Recipes)
            .ToListAsync();

        var usageDict = new Dictionary<int, decimal>();
        foreach (var oi in recentOrderItems)
        {
            if (oi.MenuItem?.Recipes == null) continue;
            foreach (var recipe in oi.MenuItem.Recipes)
            {
                decimal used = recipe.QuantityRequired * oi.Quantity;
                usageDict[recipe.IngredientId] = usageDict.GetValueOrDefault(recipe.IngredientId) + used;
            }
        }

        var ingredients = await _context.Ingredients.ToListAsync();
        var reorderItems = new List<StockReorderItem>();

        foreach (var ing in ingredients)
        {
            decimal weeklyUsage = usageDict.GetValueOrDefault(ing.IngredientId, 0m);
            decimal projectedStock = ing.CurrentStock - weeklyUsage;
            decimal suggestedReorder = 0m;

            string urgency;
            if (ing.CurrentStock <= ing.MinStock || projectedStock <= 0)
            {
                urgency = "Khẩn Cấp (Nguy Cơ Hết Hàng)";
                suggestedReorder = (ing.MinStock * 2m) - ing.CurrentStock;
                if (suggestedReorder < ing.MinStock) suggestedReorder = ing.MinStock;
            }
            else if (projectedStock <= ing.MinStock || ing.CurrentStock <= ing.MinStock * 1.5m)
            {
                urgency = "Cần Lưu Ý (Sắp Đạt Ngưỡng)";
                suggestedReorder = ing.MinStock;
            }
            else
            {
                urgency = "An Toàn";
                suggestedReorder = 0m;
            }

            string usageText = weeklyUsage > 0 ? $"{weeklyUsage:N1} {ing.Unit}/tuần" : $"0 {ing.Unit}/tuần";
            string reorderText = suggestedReorder > 0 ? $"{suggestedReorder:N1} {ing.Unit}" : "Chưa cần nhập";

            reorderItems.Add(new StockReorderItem(
                ing.Name,
                $"{ing.CurrentStock:N1} {ing.Unit}",
                $"{ing.MinStock:N1} {ing.Unit}",
                urgency,
                usageText,
                reorderText));
        }

        string advisory = await _aiService.PredictWeeklyReorderAsync();
        return new WeeklyReorderResult(reorderItems, advisory);
    }
}
