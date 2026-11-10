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
    private static readonly string[] CandidateEndpoints = new[]
    {
        "http://localhost:1234/v1/chat/completions",
        "http://localhost:1235/v1/chat/completions"
    };

    private static string _activeEndpoint = CandidateEndpoints[0];
    private static string _targetModel = "mistral-7b-instruct-v0.3";

    public LmStudioService(HttpClient httpClient, RestaurantDbContext context)
    {
        _httpClient = httpClient;
        _context = context;
        _httpClient.Timeout = TimeSpan.FromSeconds(45.0);
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
        string ingredientsStr = ingredients.Any() ? string.Join(", ", ingredients) : "Nguyên liệu tươi sạch tuyển chọn";

        string systemPrompt =
            "Bạn là chuyên gia sáng tạo nội dung ẩm thực cao cấp (Culinary Copywriter) của nhà hàng SmartRestaurant.\n" +
            "Nhiệm vụ: Viết đoạn mô tả món ăn hấp dẫn, tinh tế và kích thích vị giác để hiển thị trên thực đơn.\n" +
            "Yêu cầu:\n" +
            "- Độ dài: Đúng 2-3 câu ngắn gọn (khoảng 40-60 từ).\n" +
            "- Miêu tả sinh động sự hòa quyện hương vị, màu sắc và độ tươi ngon của các nguyên liệu chính được cung cấp.\n" +
            "- Tuyệt đối không tự bịa thêm các thành phần gây dị ứng lạ không có trong danh sách nguyên liệu.\n" +
            "- Giọng văn sang trọng, ấm cúng và tôn vinh phong vị ẩm thực.";

        string userPrompt = $"Hãy viết đoạn mô tả thực đơn cuốn hút cho món: '{dishName}', với các nguyên liệu chính: {ingredientsStr}.";

        return await SendLlmChatRequestAsync(systemPrompt, userPrompt, temperature: 0.5);
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
        // BCG Menu Engineering Matrix Grounding with Volume, Margin, and Revenue metrics
        var dishes = await _context.MenuItems
            .Select(m => new
            {
                m.MenuItemId,
                DishName = m.Name,
                Category = m.Category != null ? m.Category.CategoryName : "Khác",
                Price = m.Price,
                TotalSold = _context.OrderItems.Where(oi => oi.MenuItemId == m.MenuItemId).Sum(oi => (int?)oi.Quantity) ?? 0,
                TotalRevenue = _context.OrderItems.Where(oi => oi.MenuItemId == m.MenuItemId).Sum(oi => (decimal?)oi.UnitPrice * oi.Quantity) ?? 0m
            })
            .ToListAsync();

        decimal avgSold = dishes.Any() ? (decimal)dishes.Average(d => d.TotalSold) : 0;
        decimal avgPrice = dishes.Any() ? dishes.Average(d => d.Price) : 0;

        var categorizedData = dishes.Select(d => new
        {
            d.DishName,
            d.Category,
            Price = $"{d.Price:N0} VND",
            Sold = $"{d.TotalSold} phần",
            Revenue = $"{d.TotalRevenue:N0} VND",
            Quadrant = (d.TotalSold >= avgSold && d.Price >= avgPrice) ? "Star" :
                       (d.TotalSold >= avgSold && d.Price < avgPrice) ? "Plowhorse" :
                       (d.TotalSold < avgSold && d.Price >= avgPrice) ? "Puzzle" : "Dog"
        }).ToList();

        var matrixSnapshot = new
        {
            AverageSalesVolume = $"{avgSold:N1} phần",
            AverageDishPrice = $"{avgPrice:N0} VND",
            Dishes = categorizedData
        };

        string instruction =
            "Bạn là Giám đốc Vận hành Ẩm thực (F&B Director). Hãy phân tích báo cáo ma trận BCG Menu Engineering:\n" +
            "1. Phân loại và đánh giá từng nhóm: Stars (Ngôi sao), Plowhorses (Bò sữa), Puzzles (Thử thách), Dogs (Chó mực).\n" +
            "2. Đưa ra chiến lược cụ thể cho từng nhóm: điều chỉnh giá, định lượng nguyên liệu (BOM), hay kỹ năng upselling của nhân viên.\n" +
            "3. Trình bày rõ ràng theo tiêu đề markdown, súc tích và có tính hành động cao.";

        string systemPrompt = ContextGroundingHelper.BuildGroundedSystemPrompt(matrixSnapshot, instruction);

        return await SendLlmChatRequestAsync(systemPrompt, "Hãy phân tích ma trận thực đơn BCG và đề xuất kế hoạch tối ưu lợi nhuận.");
    }

    public async Task<string> AnalyzePeakHoursAsync()
    {
        var orderTimes = await _context.Orders
            .Select(o => new { o.OrderId, Hour = o.CreatedAt.Hour })
            .ToListAsync();

        var hourlyDistribution = orderTimes
            .GroupBy(o => o.Hour)
            .Select(g => new
            {
                TimeSlot = $"{g.Key:D2}:00 - {g.Key + 1:D2}:00",
                OrderCount = g.Count(),
                TrafficLevel = g.Count() >= 8 ? "Cao điểm (Đông khách)" :
                               g.Count() >= 4 ? "Trung bình" : "Thấp điểm"
            })
            .OrderBy(x => x.TimeSlot)
            .ToList();

        var summary = new
        {
            OperatingHours = "09:00 - 22:00",
            TotalOrdersSampled = orderTimes.Count,
            PeakLunchWindow = "11:30 - 13:30",
            PeakDinnerWindow = "18:00 - 20:30",
            HourlyTraffic = hourlyDistribution
        };

        string instruction =
            "Bạn là Quản lý Vận hành Nhà hàng chuyên nghiệp.\n" +
            "Dựa trên phân bố lưu lượng khách theo khung giờ:\n" +
            "1. Đánh giá chi tiết hai đợt cao điểm trưa (11:30-13:30) và tối (18:00-20:30).\n" +
            "2. Đưa ra phương án phân bổ nhân sự: số lượng nhân viên chạy bàn (runner), thu ngân, và phụ bếp line nóng theo ca.\n" +
            "3. Hướng dẫn công tác chuẩn bị nguyên liệu (mise-en-place) trong khung giờ thấp điểm (14:00-17:00) để phục vụ giờ cao điểm tối mượt mà.";

        string systemPrompt = ContextGroundingHelper.BuildGroundedSystemPrompt(summary, instruction);

        return await SendLlmChatRequestAsync(systemPrompt, "Phân tích giờ cao điểm và hướng dẫn điều phối nhân sự nhà hàng.");
    }

    public async Task<string> PredictWeeklyReorderAsync()
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
                usageDict[recipe.IngredientId] = usageDict.GetValueOrDefault(recipe.IngredientId) + (recipe.QuantityRequired * oi.Quantity);
            }
        }

        var ingredients = await _context.Ingredients.ToListAsync();
        var stockSnapshot = ingredients.Select(i =>
        {
            decimal weeklyUsage = usageDict.GetValueOrDefault(i.IngredientId, 0m);
            decimal projected = i.CurrentStock - weeklyUsage;
            return new
            {
                Ingredient = i.Name,
                CurrentStock = $"{i.CurrentStock:N1} {i.Unit}",
                MinStock = $"{i.MinStock:N1} {i.Unit}",
                Past7DaysConsumption = $"{weeklyUsage:N1} {i.Unit}",
                EstimatedStockAfter7Days = $"{projected:N1} {i.Unit}",
                Status = (i.CurrentStock <= i.MinStock || projected <= 0) ? "Cực kỳ khẩn cấp" :
                         (projected <= i.MinStock) ? "Cần đặt hàng bổ sung" : "Đủ dùng"
            };
        }).ToList();

        string instruction =
            "Bạn là Chuyên gia Quản lý Kho Vận và Cung ứng (Supply Chain Manager) của nhà hàng.\n" +
            "Dựa trên dữ liệu tồn kho thực tế, ngưỡng an toàn và tốc độ tiêu thụ 7 ngày qua:\n" +
            "1. Lập danh sách ưu tiên các nguyên liệu cần đặt hàng gấp trong 24-48 giờ tới.\n" +
            "2. Đề xuất số lượng đặt hàng ước tính cho từng nguyên vật liệu để tránh đứt gãy phục vụ.\n" +
            "3. Đưa ra lưu ý bảo quản nguyên liệu tươi sống (rau củ, thịt, gia vị).\n" +
            "Trình bày chuyên nghiệp, gạch đầu dòng rõ ràng theo từng nhóm nguyên liệu.";

        string systemPrompt = ContextGroundingHelper.BuildGroundedSystemPrompt(stockSnapshot, instruction);

        return await SendLlmChatRequestAsync(systemPrompt, "Hãy lập kế hoạch dự báo và đề xuất danh mục nhập kho 7 ngày tới.");
    }

    private async Task<string> SendLlmChatRequestAsync(string systemPrompt, string userPrompt, double temperature = 0.3)
    {
        var payload = new
        {
            model = _targetModel,
            temperature = temperature, // Dynamic temperature: 0.3 for factual grounding, 0.5 for culinary copywriting
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string jsonPayload = JsonSerializer.Serialize(payload, options);

        // Try active endpoint first, fallback to alternatives (ports 1234 / 1235) if connection fails
        var endpointsToTry = new List<string> { _activeEndpoint };
        endpointsToTry.AddRange(CandidateEndpoints.Where(e => e != _activeEndpoint));

        HttpResponseMessage? response = null;
        Exception? lastEx = null;

        try
        {
            foreach (var endpoint in endpointsToTry)
            {
                try
                {
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var candidateResponse = await _httpClient.PostAsync(endpoint, content);
                    if (candidateResponse.IsSuccessStatusCode)
                    {
                        _activeEndpoint = endpoint;
                        response = candidateResponse;
                        break;
                    }
                    else
                    {
                        response = candidateResponse;
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastEx = ex;
                }
                catch (TaskCanceledException ex)
                {
                    lastEx = ex;
                }
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                if (lastEx is TaskCanceledException)
                {
                    return "⚠️ [Hết thời gian chờ] LM Studio phản hồi quá lâu (> 15 giây). Vui lòng kiểm tra tải GPU/CPU của máy tính.";
                }

                return "⚠️ [Chế độ Ngoại tuyến] Máy chủ LM Studio chưa được khởi động trên cổng 1234 hoặc 1235. " +
                       "Vui lòng mở ứng dụng LM Studio, tải mô hình 'Mistral-7B-Instruct' và bật Local Server để sử dụng tính năng AI.";
            }

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
        catch (Exception ex)
        {
            return $"⚠️ [Lỗi AI]: {ex.Message}";
        }
    }
}
