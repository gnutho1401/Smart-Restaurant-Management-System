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

    public async Task<WeeklyReorderResult> ComputeWeeklyReorderForecastAsync()
    {
        var ingredients = await _context.Ingredients.ToListAsync();
        var reorderItems = new List<StockReorderItem>();

        foreach (var ing in ingredients)
        {
            string urgency;
            if (ing.CurrentStock <= ing.MinStock)
                urgency = "Khẩn Cấp (Dưới Định Mức)";
            else if (ing.CurrentStock <= ing.MinStock * 1.5m)
                urgency = "Cần Lưu Ý";
            else
                urgency = "An Toàn";

            reorderItems.Add(new StockReorderItem(
                ing.Name,
                $"{ing.CurrentStock:N1} {ing.Unit}",
                $"{ing.MinStock:N1} {ing.Unit}",
                urgency));
        }

        string advisory = await _aiService.PredictWeeklyReorderAsync();
        return new WeeklyReorderResult(reorderItems, advisory);
    }
}
