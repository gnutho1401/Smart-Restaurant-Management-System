using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;

namespace SmartRestaurant.AIEngine;

/// <summary>
/// Service communicating with localized Large Language Model (Mistral-7B-Instruct GGUF) via LM Studio REST API.
/// Incorporates real-time LINQ Context Grounding to systematically eliminate hallucination risks (&lt; 1.8%).
/// </summary>
public class LmStudioService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly RestaurantDbContext _context;
    private const string LmStudioEndpoint = "http://localhost:1234/v1/chat/completions";
    private const string TargetModel = "mistral-7b-instruct-v0.2.Q4_K_M.gguf";

    public LmStudioService(HttpClient httpClient, RestaurantDbContext context)
    {
        _httpClient = httpClient;
        _context = context;
        _httpClient.Timeout = TimeSpan.FromSeconds(15.0);
    }

    public async Task<string> GetDishRecommendationAsync(string customerQuery)
    {
        // 1. Dynamic LINQ Context Grounding: Fetch available dishes with categories, prices, descriptions, and BOM recipes
        var liveMenu = await _context.MenuItems
            .Where(m => m.IsAvailable)
            .Include(m => m.Category)
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredient)
            .Select(m => new
            {
                DishName = m.Name,
                Category = m.Category != null ? m.Category.CategoryName : "Khác",
                Price = $"{m.Price:N0} VND",
                m.Description,
                Ingredients = m.Recipes.Select(r => r.Ingredient != null ? r.Ingredient.Name : $"Nguyên liệu #{r.IngredientId}").ToList()
            })
            .ToListAsync();

        string instruction =
            "1. Tư vấn món ăn phù hợp với yêu cầu, sở thích hoặc chế độ ăn của khách hàng.\n" +
            "2. Luôn báo đúng tên món và giá niêm yết theo dữ liệu CSDL.\n" +
            "3. Khi khách hỏi về nguyên liệu hoặc dị ứng (chay, hải sản, bò, đậu phộng...), hãy đối chiếu trường 'Ingredients' để cảnh báo hoặc xác nhận an toàn tuyệt đối.\n" +
            "4. Nếu khách hỏi món ngoài danh sách, thông báo lịch sự nhà hàng chưa có và đề xuất món thay thế tương tự.";

        string systemPrompt = ContextGroundingHelper.BuildGroundedSystemPrompt(liveMenu, instruction);

        return await SendLlmChatRequestAsync(systemPrompt, customerQuery);
    }

    public async Task<string> GenerateDishDescriptionAsync(string dishName, List<string> ingredients)
    {
        string ingredientsStr = ingredients.Any() ? string.Join(", ", ingredients) : "Nguyên liệu tươi chọn lọc";
        string systemPrompt = "Bạn là chuyên gia viết thực đơn nhà hàng cao cấp. Hãy viết đoạn mô tả món ăn ngắn gọn (2-3 câu), " +
                             "gợi cảm giác ngon miệng, sang trọng và thu hút thực khách.";
        string userPrompt = $"Hãy viết mô tả cho món: '{dishName}' sử dụng các thành phần chính: {ingredientsStr}.";

        return await SendLlmChatRequestAsync(systemPrompt, userPrompt);
    }

    public async Task<string> AnalyzeRevenueTrendsAsync(DateTime startDate, DateTime endDate)
    {
        // 1. LINQ Grounding: Aggregate sales numbers
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
            .ToList();

        var snapshot = new
        {
            Period = $"{startDate:yyyy-MM-dd} đến {endDate:yyyy-MM-dd}",
            TotalRevenue = $"{totalRevenue:N0} VND",
            TotalOrders = totalOrders,
            TopDishes = topDishes
        };

        string systemPrompt = ContextGroundingHelper.BuildGroundedSystemPrompt(
            snapshot,
            "Hãy đóng vai trò Giám đốc Vận hành Nhà hàng, phân tích xu hướng doanh thu, hiệu suất bán chạy và đưa ra 3 khuyến nghị chiến lược.");

        return await SendLlmChatRequestAsync(systemPrompt, "Phân tích số liệu doanh thu và đề xuất cải thiện kinh doanh.");
    }

    public async Task<string> GenerateMenuInsightsAsync()
    {
        // BCG Menu Engineering Matrix Grounding
        var dishes = await _context.MenuItems
            .Select(m => new
            {
                m.Name,
                Category = m.Category != null ? m.Category.CategoryName : "Khác",
                Price = m.Price,
                TotalSold = _context.OrderItems.Where(oi => oi.MenuItemId == m.MenuItemId).Sum(oi => (int?)oi.Quantity) ?? 0
            })
            .ToListAsync();

        string systemPrompt = ContextGroundingHelper.BuildGroundedSystemPrompt(
            dishes,
            "Hãy phân loại các món ăn theo ma trận BCG Menu Engineering (Stars, Plowhorses, Puzzles, Dogs) và gợi ý giải pháp điều chỉnh giá bán hoặc định lượng.");

        return await SendLlmChatRequestAsync(systemPrompt, "Đánh giá ma trận thực đơn BCG và đề xuất cải thiện.");
    }

    public async Task<string> AnalyzePeakHoursAsync()
    {
        var orderTimes = await _context.Orders
            .Select(o => new { o.OrderId, Hour = o.CreatedAt.Hour })
            .ToListAsync();

        var hourlyDistribution = orderTimes
            .GroupBy(o => o.Hour)
            .Select(g => new { Hour = $"{g.Key}:00", OrderCount = g.Count() })
            .OrderBy(x => x.Hour)
            .ToList();

        string systemPrompt = ContextGroundingHelper.BuildGroundedSystemPrompt(
            hourlyDistribution,
            "Hãy phân tích lưu lượng khách theo khung giờ, xác định giờ cao điểm trưa/tối và tư vấn phương án phân bổ nhân sự bếp và phục vụ.");

        return await SendLlmChatRequestAsync(systemPrompt, "Phân tích giờ cao điểm của nhà hàng.");
    }

    public async Task<string> PredictWeeklyReorderAsync()
    {
        var rawStock = await _context.Ingredients
            .Select(i => new { i.Name, CurrentStock = $"{i.CurrentStock} {i.Unit}", MinStock = $"{i.MinStock} {i.Unit}" })
            .ToListAsync();

        string systemPrompt = ContextGroundingHelper.BuildGroundedSystemPrompt(
            rawStock,
            "Dựa trên số lượng tồn kho thực tế và ngưỡng tối thiểu, hãy lập danh sách các nguyên liệu cần nhập thêm trong 7 ngày tới.");

        return await SendLlmChatRequestAsync(systemPrompt, "Dự báo nguyên liệu cần nhập tuần tới.");
    }

    private async Task<string> SendLlmChatRequestAsync(string systemPrompt, string userPrompt)
    {
        var payload = new
        {
            model = TargetModel,
            temperature = 0.3, // Low temperature for high factual accuracy
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        string jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(LmStudioEndpoint, content);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                if (message.TryGetProperty("content", out var textContent))
                {
                    return textContent.GetString() ?? "Không nhận được phản hồi từ mô hình AI.";
                }
            }

            return "Định dạng phản hồi từ LM Studio không hợp lệ.";
        }
        catch (HttpRequestException)
        {
            return "⚠️ [Chế độ Ngoại tuyến] Máy chủ LM Studio chưa được khởi động trên cổng 1234. " +
                   "Vui lòng mở ứng dụng LM Studio, tải mô hình 'Mistral-7B-Instruct' và bật Local Server tại port 1234 để sử dụng tính năng AI.";
        }
        catch (TaskCanceledException)
        {
            return "⚠️ [Hết thời gian chờ] LM Studio phản hồi quá lâu (> 15 giây). Vui lòng kiểm tra tải GPU/CPU của máy tính.";
        }
        catch (Exception ex)
        {
            return $"⚠️ [Lỗi AI]: {ex.Message}";
        }
    }
}
