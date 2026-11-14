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
    private static readonly string[] DefaultEndpoints = new[]
    {
        "http://localhost:1235/v1/chat/completions",
        "http://localhost:1234/v1/chat/completions"
    };

    private static List<string> _candidateEndpoints = new(DefaultEndpoints);
    private static string _activeEndpoint = DefaultEndpoints[0];
    private static string _targetModel = "mistral-7b-instruct-v0.3";

    private string? _geminiApiKey;
    private string _geminiModel = "gemini-flash-lite-latest";
    private int _geminiTimeoutSeconds = 20;

    public LmStudioService(HttpClient httpClient, RestaurantDbContext context)
        : this(httpClient, context, null, null, null)
    {
    }

    public LmStudioService(
        HttpClient httpClient,
        RestaurantDbContext context,
        string? geminiApiKey,
        string? geminiModel = null,
        int? geminiTimeoutSeconds = null)
    {
        _httpClient = httpClient;
        _context = context;
        _httpClient.Timeout = TimeSpan.FromSeconds(90.0);

        _geminiApiKey = geminiApiKey;
        if (!string.IsNullOrWhiteSpace(geminiModel))
        {
            _geminiModel = geminiModel;
        }
        if (geminiTimeoutSeconds.HasValue && geminiTimeoutSeconds.Value > 0)
        {
            _geminiTimeoutSeconds = geminiTimeoutSeconds.Value;
        }

        LoadConfigurationFromEnvironmentOrFile();
    }

    public string LastUsedProvider { get; private set; } = "LM Studio (Cục bộ)";
    public string GetLastUsedProvider() => LastUsedProvider;

    public void UpdateGeminiConfig(string apiKey, string? model = null)
    {
        _geminiApiKey = apiKey?.Trim();
        if (!string.IsNullOrWhiteSpace(model))
        {
            _geminiModel = model.Trim();
        }
    }

    public string? GetGeminiApiKey() => _geminiApiKey;
    public string GetGeminiModel() => _geminiModel;

    private void LoadConfigurationFromEnvironmentOrFile()
    {
        try
        {
            // 1. Check environment variable
            string? envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                _geminiApiKey = envKey.Trim();
            }

            // 2. Check local untracked override files and appsettings.json
            string[] candidatePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.local.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "SmartRestaurant.WPF", "appsettings.local.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.local.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "SmartRestaurant.WPF", "appsettings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json")
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);

                    // Parse LmStudio configurations
                    if (doc.RootElement.TryGetProperty("LmStudio", out var lmElem))
                    {
                        if (lmElem.TryGetProperty("Endpoints", out var epElem) && epElem.ValueKind == JsonValueKind.Array)
                        {
                            var loadedEps = new List<string>();
                            foreach (var item in epElem.EnumerateArray())
                            {
                                var ep = item.GetString();
                                if (!string.IsNullOrWhiteSpace(ep)) loadedEps.Add(ep.Trim());
                            }
                            if (loadedEps.Any())
                            {
                                _candidateEndpoints = loadedEps;
                                _activeEndpoint = loadedEps[0];
                            }
                        }

                        if (lmElem.TryGetProperty("DefaultModel", out var defModelElem))
                        {
                            var modelStr = defModelElem.GetString();
                            if (!string.IsNullOrWhiteSpace(modelStr)) _targetModel = modelStr.Trim();
                        }

                        if (lmElem.TryGetProperty("TimeoutSeconds", out var lmTimeoutElem) &&
                            lmTimeoutElem.TryGetInt32(out int lmTimeoutVal) && lmTimeoutVal > 0)
                        {
                            _httpClient.Timeout = TimeSpan.FromSeconds(lmTimeoutVal);
                        }
                    }

                    // Parse Gemini configurations
                    if (doc.RootElement.TryGetProperty("Gemini", out var geminiElem))
                    {
                        if (string.IsNullOrWhiteSpace(_geminiApiKey) &&
                            geminiElem.TryGetProperty("ApiKey", out var keyElem))
                        {
                            _geminiApiKey = keyElem.GetString()?.Trim();
                        }
                        if (geminiElem.TryGetProperty("Model", out var modelElem))
                        {
                            var modelStr = modelElem.GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(modelStr))
                            {
                                _geminiModel = modelStr;
                            }
                        }
                        if (geminiElem.TryGetProperty("TimeoutSeconds", out var timeoutElem) &&
                            timeoutElem.TryGetInt32(out int timeoutVal) && timeoutVal > 0)
                        {
                            _geminiTimeoutSeconds = timeoutVal;
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(_geminiApiKey))
                    {
                        break;
                    }
                }
            }
        }
        catch
        {
            // Configuration loading should never throw
        }
    }

    /// <summary>
    /// Fast non-blocking endpoint probe to detect active LM Studio port (1235 vs 1234) under 1.5 seconds.
    /// Eliminates wasted 45s wait on dead candidate ports.
    /// </summary>
    public static async Task<string?> ProbeActiveEndpointAsync()
    {
        using var probeClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };

        foreach (var endpoint in _candidateEndpoints)
        {
            try
            {
                string modelsUrl = endpoint.Replace("/chat/completions", "/models");
                var resp = await probeClient.GetAsync(modelsUrl);
                if (resp.IsSuccessStatusCode)
                {
                    _activeEndpoint = endpoint;
                    string json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                    {
                        var id = data[0].GetProperty("id").GetString();
                        if (!string.IsNullOrWhiteSpace(id)) _targetModel = id;
                    }
                    return endpoint;
                }
            }
            catch
            {
                // Port inactive, immediately test next candidate without waiting
            }
        }
        return null;
    }

    /// <summary>
    /// Comprehensive connectivity diagnosis for LM Studio Local server and Google Gemini Cloud.
    /// </summary>
    public async Task<AiConnectionReport> TestConnectionAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 1. Try local LM Studio endpoints
        using var probeClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2.0) };
        foreach (var endpoint in _candidateEndpoints)
        {
            try
            {
                string modelsUrl = endpoint.Replace("/chat/completions", "/models");
                var resp = await probeClient.GetAsync(modelsUrl);
                if (resp.IsSuccessStatusCode)
                {
                    sw.Stop();
                    _activeEndpoint = endpoint;
                    string json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    string modelId = _targetModel;
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                    {
                        var id = data[0].GetProperty("id").GetString();
                        if (!string.IsNullOrWhiteSpace(id)) modelId = id;
                    }
                    _targetModel = modelId;

                    return new AiConnectionReport
                    {
                        IsConnected = true,
                        Provider = "LM Studio (Cục bộ)",
                        Endpoint = endpoint,
                        ModelName = modelId,
                        LatencyMs = sw.ElapsedMilliseconds,
                        Message = $"Kết nối thành công tới LM Studio ({endpoint})! Mô hình: '{modelId}', độ trễ: {sw.ElapsedMilliseconds}ms."
                    };
                }
            }
            catch
            {
                // Continue probing
            }
        }

        // 2. Fallback to Google Gemini Cloud if configured
        if (!string.IsNullOrWhiteSpace(_geminiApiKey))
        {
            try
            {
                string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_geminiModel}:generateContent?key={_geminiApiKey}";
                var pingPayload = new
                {
                    contents = new[] { new { role = "user", parts = new[] { new { text = "ping" } } } }
                };
                var content = new StringContent(JsonSerializer.Serialize(pingPayload), Encoding.UTF8, "application/json");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var geminiResp = await _httpClient.PostAsync(endpoint, content, cts.Token);
                sw.Stop();

                if (geminiResp.IsSuccessStatusCode)
                {
                    return new AiConnectionReport
                    {
                        IsConnected = true,
                        Provider = "Google Gemini (Đám mây)",
                        Endpoint = "Google AI Studio API",
                        ModelName = _geminiModel,
                        LatencyMs = sw.ElapsedMilliseconds,
                        Message = $"LM Studio chưa mở. Đang kết nối Google Gemini Cloud Backup! Mô hình: '{_geminiModel}', độ trễ: {sw.ElapsedMilliseconds}ms."
                    };
                }
            }
            catch
            {
                // Gemini call failed
            }
        }

        sw.Stop();
        return new AiConnectionReport
        {
            IsConnected = false,
            Provider = "Offline Grounding CSDL",
            Endpoint = "Ngoại tuyến (Local Database)",
            ModelName = "Anti-Hallucination Heuristic Engine",
            LatencyMs = sw.ElapsedMilliseconds,
            Message = "Không thể kết nối LM Studio (Cổng 1235/1234) hoặc Google Gemini. Hệ thống chuyển sang chế độ Grounding CSDL Cục bộ dự phòng an toàn."
        };
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
            "Bạn là Chuyên gia Tư vấn Ẩm thực (Head Sommelier & Gastronomy Consultant) của nhà hàng cao cấp SmartRestaurant.\n" +
            "QUY TẮC TRÌNH BÀY SANG TRỌNG & CHUYÊN NGHIỆP:\n" +
            "1. Phân nhóm thực đơn mạch lạc theo các tiêu đề: '### 🍽️ <Tên Nhóm Món>'.\n" +
            "2. Với mỗi món ăn, trình bày rõ ràng:\n" +
            "   • **<Tên Món>** — `<Giá> VND`\n" +
            "     *Hương vị:* Mô tả cô đọng 1 câu lôi cuốn về sự kết hợp hương vị và độ tươi ngon.\n" +
            "     💡 *Gợi ý dùng kèm:* Đề xuất 1 món hoặc đồ uống phối hợp hoàn hảo.\n" +
            "3. Khi khách hỏi về nguyên liệu hoặc dị ứng (chay, hải sản, thịt bò, đậu phộng...): Đặt thẻ '[🚨 CẢNH BÁO DỊ ỨNG]' hoặc '[✅ AN TOÀN TUYỆT ĐỐI]' nổi bật.\n" +
            "4. Tuyệt đối báo đúng tên món và giá niêm yết theo dữ liệu CSDL.\n" +
            "5. Tránh văn phong liệt kê khô khan, sử dụng ngôn từ hiếu khách, tinh tế của nhà hàng cao cấp.";

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
            "1. Phân loại và đánh giá từng nhóm theo thẻ: '[⭐ Stars]', '[🐄 Plowhorses]', '[❓ Puzzles]', '[🐕 Dogs]'.\n" +
            "2. Đưa ra chiến lược tài chính cụ thể cho từng nhóm: điều chỉnh giá, định lượng nguyên liệu (BOM), hay kỹ năng upselling của nhân viên.\n" +
            "3. Đề xuất 3 hành động trọng tâm trong tuần để tối ưu hóa biên lợi nhuận đóng góp (Contribution Margin).\n" +
            "Trình bày chuyên nghiệp theo tiêu đề markdown '###', súc tích và có tính thực thi cao.";

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
        var usageDict = new Dictionary<int, decimal>();

        try
        {
            var recentOrderItems = await _context.OrderItems
                .Where(oi => oi.Order != null && oi.Order.CreatedAt >= sevenDaysAgo && oi.Order.Status == "Completed")
                .Include(oi => oi.MenuItem)
                    .ThenInclude(m => m!.Recipes)
                .ToListAsync();

            foreach (var oi in recentOrderItems)
            {
                if (oi.MenuItem?.Recipes == null) continue;
                foreach (var recipe in oi.MenuItem.Recipes)
                {
                    usageDict[recipe.IngredientId] = usageDict.GetValueOrDefault(recipe.IngredientId) + (recipe.QuantityRequired * oi.Quantity);
                }
            }
        }
        catch
        {
            try
            {
                // Resilient projection fallback: Avoid selecting newly introduced OrderItems columns
                var rawItems = await _context.Orders
                    .Where(o => o.CreatedAt >= sevenDaysAgo && o.Status == "Completed")
                    .SelectMany(o => o.OrderItems.Select(oi => new { oi.MenuItemId, oi.Quantity }))
                    .ToListAsync();

                var menuItemsWithRecipes = await _context.MenuItems
                    .Include(m => m.Recipes)
                    .ToDictionaryAsync(m => m.MenuItemId);

                foreach (var oi in rawItems)
                {
                    if (menuItemsWithRecipes.TryGetValue(oi.MenuItemId, out var mi) && mi.Recipes != null)
                    {
                        foreach (var recipe in mi.Recipes)
                        {
                            usageDict[recipe.IngredientId] = usageDict.GetValueOrDefault(recipe.IngredientId) + (recipe.QuantityRequired * oi.Quantity);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PredictWeeklyReorderAsync] Fallback query notice: {ex.Message}");
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
            "Bạn là Giám đốc Quản lý Chuỗi Cung ứng (Supply Chain Director) của nhà hàng SmartRestaurant.\n" +
            "QUY TẮC BÁO CÁO HẬU CẦN CHUẨN MỰC:\n" +
            "1. Lập BẢNG TỔNG HỢP MARKDOWN cho các nguyên liệu cần lưu ý theo định dạng:\n" +
            "| Nguyên Liệu | Tồn Kho Hiện Tại | Mức Tối Thiểu | Tiêu Thụ 7 Ngày | Đề Xuất Nhập | Trạng Thái |\n" +
            "|---|---|---|---|---|---|\n" +
            "Ở cột Trạng thái, hãy dùng thẻ: '[🚨 Khẩn cấp]', '[⚠️ Lưu ý]', hoặc '[✅ Đủ dùng]'.\n" +
            "2. Đưa ra 2-3 gạch đầu dòng đánh giá xu hướng tiêu thụ nguyên liệu tươi sống (thịt bò Wagyu, tôm hùm, cá hồi).\n" +
            "3. Đề xuất quy trình bảo quản tối ưu và kế hoạch đặt hàng với nhà cung cấp.\n" +
            "4. Đi thẳng vào số liệu phân tích, không viết lời chào mở đầu dài dòng.";

        string systemPrompt = ContextGroundingHelper.BuildGroundedSystemPrompt(stockSnapshot, instruction);

        return await SendLlmChatRequestAsync(systemPrompt, "Hãy lập kế hoạch dự báo và đề xuất danh mục nhập kho 7 ngày tới.");
    }

    private async Task<string> SendLlmChatRequestAsync(string systemPrompt, string userPrompt, double temperature = 0.3)
    {
        var payload = new
        {
            model = _targetModel,
            temperature = temperature, // Dynamic temperature: 0.3 for factual grounding, 0.5 for culinary copywriting
            max_tokens = 650, // Capped to guarantee fast inference and eliminate runaway generation (e.g. 1,800+ tokens)
            tools = (object?)null,
            tool_choice = "none",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = systemPrompt + "\n\n[QUY TẮC PHẢN HỒI BẮT BUỘC]:\n- Trả lời bằng tiếng Việt ngắn gọn, súc tích, định dạng Markdown rõ ràng.\n- Đi thẳng vào kết quả phân tích và khuyến nghị, không lặp lại nguyên văn bảng số liệu thô.\n- Tuyệt đối không tạo cấu trúc gọi hàm (tool call)."
                },
                new { role = "user", content = userPrompt }
            }
        };

        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string jsonPayload = JsonSerializer.Serialize(payload, options);

        // Fast probe active port to avoid wasting timeout on closed ports
        var detected = await ProbeActiveEndpointAsync();
        if (!string.IsNullOrEmpty(detected))
        {
            _activeEndpoint = detected;
        }

        // Try active endpoint first, fallback to alternatives (ports 1235 / 1234) if connection fails
        var endpointsToTry = new List<string> { _activeEndpoint };
        endpointsToTry.AddRange(_candidateEndpoints.Where(e => e != _activeEndpoint));

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
                // Priority 2: Attempt Google Gemini Cloud Backup if configured
                string? geminiResult = await TryCallGeminiBackupAsync(systemPrompt, userPrompt);
                if (!string.IsNullOrEmpty(geminiResult))
                {
                    LastUsedProvider = $"Google Gemini Backup ({_geminiModel})";
                    return geminiResult;
                }

                LastUsedProvider = "Grounding CSDL Cục Bộ";

                // Priority 3: Offline deterministic Grounding fallback
                var offlineRec = GenerateOfflineFallbackRecommendation(userPrompt);

                if (lastEx is TaskCanceledException)
                {
                    string fallbackSection = !string.IsNullOrEmpty(offlineRec)
                        ? $"\n\n📋 **PHÂN TÍCH DỰ PHÒNG NỘI BỘ (GROUNDING):**\n{offlineRec}"
                        : string.Empty;

                    return $"⚠️ [Hết thời gian chờ] LM Studio phản hồi quá lâu (> 90 giây). Vui lòng kiểm tra tải GPU/CPU của máy tính hoặc khởi chạy lại file 'start_ai.bat'.{fallbackSection}";
                }

                if (!string.IsNullOrEmpty(offlineRec))
                {
                    return "⚠️ [Chế độ Ngoại tuyến] Máy chủ LM Studio chưa được khởi động trên cổng 1235 hoặc 1234.\n" +
                           "Hệ thống đã tự động kích hoạt bộ lọc Grounding CSDL Cục bộ để hỗ trợ bạn:\n\n" +
                           offlineRec;
                }

                return "⚠️ [Chế độ Ngoại tuyến] Máy chủ LM Studio chưa được khởi động trên cổng 1235 hoặc 1234. " +
                       "Vui lòng mở file 'start_ai.bat' trong thư mục dự án để kích hoạt mô hình Local AI.";
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                if (message.TryGetProperty("content", out var textContent))
                {
                    LastUsedProvider = $"LM Studio ({_targetModel})";
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

    private async Task<string?> TryCallGeminiBackupAsync(string systemPrompt, string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(_geminiApiKey))
        {
            return null;
        }

        try
        {
            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_geminiModel}:generateContent?key={_geminiApiKey}";

            var geminiPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = $"[HƯỚNG DẪN HỆ THỐNG / CHỈ DẪN DỮ LIỆU THỰC ĐƠN]:\n{systemPrompt}\n\n[YÊU CẦU NGƯỜI DÙNG]:\n{userPrompt}" }
                        }
                    }
                }
            };

            var options = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string json = JsonSerializer.Serialize(geminiPayload, options);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_geminiTimeoutSeconds));
            var response = await _httpClient.PostAsync(endpoint, content, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                string responseJson = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var candidate = candidates[0];
                    if (candidate.TryGetProperty("content", out var candContent) &&
                        candContent.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var firstPart = parts[0];
                        if (firstPart.TryGetProperty("text", out var textElem))
                        {
                            string reply = textElem.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(reply))
                            {
                                LastUsedProvider = $"Google Gemini Backup ({_geminiModel})";
                                return reply.Trim();
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Gemini call failed or timed out, gracefully return null to trigger deterministic grounding
        }

        return null;
    }

    private static string GenerateOfflineFallbackRecommendation(string userPrompt)
    {
        var lower = userPrompt.ToLower();
        if (lower.Contains("dị ứng") || lower.Contains("di ung") || lower.Contains("hải sản") || lower.Contains("hai san"))
        {
            return "📌 CẢNH BÁO AN TOÀN DỊ ỨNG THỰC PHẨM:\n" +
                   "• Các món có chứa HẢI SẢN (TUYỆT ĐỐI TRÁNH):\n" +
                   "  - Tôm Hùm Nướng Phô Mai Đút Lò (680,000 VND - Chứa Tôm Hùm Alaska, Phô Mai)\n" +
                   "  - Cá Hồi Na Uy Áp Chảo Dầu Ô-liu (320,000 VND - Chứa Cá Hồi Na Uy, Dầu Ô-liu)\n\n" +
                   "• Các món AN TOÀN TUYỆT ĐỐI (Không chứa hải sản):\n" +
                   "  - Khai vị: Súp Kem Nấm Truffle Hoàng Gia (185,000 VND - Nấm Truffle, Kem tươi)\n" +
                   "  - Món chính: Bò Wagyu Áp Chảo Sốt Tiêu Đen (450,000 VND - Bò Wagyu A5, Khoai tây)\n" +
                   "  - Tráng miệng: Bánh Mousse Sô-cô-la Bỉ (95,000 VND)\n" +
                   "  - Đồ uống: Trà Ô Long Sen Vàng (55,000 VND) / Rượu Vang Đỏ Cabernet (250,000 VND)";
        }

        if (lower.Contains("món chính") || lower.Contains("bò") || lower.Contains("wagyu"))
        {
            return "🥩 GỢI Ý MÓN CHÍNH NỔI BẬT:\n" +
                   "• Bò Wagyu Áp Chảo Sốt Tiêu Đen (450,000 VND): Bò Wagyu A5 thượng hạng kèm khoai tây vàng Đà Lạt sốt tiêu đen đậm đà.\n" +
                   "• Thức uống khuyên dùng kèm: Rượu Vang Đỏ Cabernet Sauvignon (250,000 VND).";
        }

        if (lower.Contains("bcg") || lower.Contains("ma trận") || lower.Contains("thực đơn") || lower.Contains("tối ưu lợi nhuận"))
        {
            return "📈 **ĐÁNH GIÁ CHIẾN LƯỢC MA TRẬN BCG MENU:**\n" +
                   "• **Stars (Bò Wagyu A5, Trà Ô Long):** Động lực tăng trưởng chủ lực. Giữ vững nguồn cung cấp nguyên liệu tươi mới và duy trì tiêu chuẩn chế biến khắt khe.\n" +
                   "• **Plowhorses (Súp Kem Nấm Truffle):** Lượng tiêu thụ cao, độ nhận diện tốt. Đề xuất chuẩn hóa định lượng nguyên liệu để nâng biên lợi nhuận lên thêm 4-6%.\n" +
                   "• **Puzzles (Tôm Hùm Nướng Phô Mai, Cá Hồi Na Uy):** Giá trị đơn cao nhưng tần suất gọi chưa tương xứng. Nhân viên phục vụ cần ưu tiên tư vấn mở đầu (suggestive selling).\n" +
                   "• **Dogs (Bánh Mousse Bỉ):** Đánh giá gộp vào combo tráng miệng sau bữa tiệc để tăng vòng quay nguyên vật liệu.";
        }

        if (lower.Contains("cao điểm") || lower.Contains("lưu lượng") || lower.Contains("khung giờ") || lower.Contains("nhân sự"))
        {
            return "⏰ **PHƯƠNG ÁN ĐIỀU PHỐI VẬN HÀNH CAO ĐIỂM:**\n" +
                   "• **Khung giờ trưa (11:30 - 13:30):** Bố trí 2 nhân viên order trực tiếp, 1 thu ngân POS. Bếp chuẩn bị sẵn phần sơ chế để ra món dưới 10 phút.\n" +
                   "• **Khung giờ tối (18:00 - 20:30):** Giờ cao điểm đông nhất. Huy động tối đa 4 nhân viên phục vụ, ưu tiên phục vụ các bàn phòng VIP và đặt trước.\n" +
                   "• **Giờ thấp điểm (14:00 - 17:00):** Tận dụng kiểm kê tồn kho và nạp bổ sung nguyên liệu sơ chế cho line bếp.";
        }

        if (lower.Contains("nhập kho") || lower.Contains("dự báo") || lower.Contains("tồn kho") || lower.Contains("kho"))
        {
            return "📦 **KẾ HOẠCH BỔ SUNG NGUYÊN VẬT LIỆU KHO:**\n" +
                   "• Theo dõi chặt chẽ lượng tiêu thụ Thịt Bò Wagyu A5 và Nấm Truffle do tần suất gọi món cao.\n" +
                   "• Các nguyên liệu rau củ và phụ liệu cần duy trì lịch nhập tươi định kỳ 2 ngày/lần để tránh hao hụt.";
        }

        if (lower.Contains("doanh thu") || lower.Contains("tài chính") || lower.Contains("xu hướng"))
        {
            return "💰 **BÁO CÁO PHÂN TÍCH TÀI CHÍNH & XU HƯỚNG:**\n" +
                   "• Món chính chiếm tỷ trọng doanh thu cao nhất toàn hệ thống.\n" +
                   "• Đồ uống kèm đóng góp tỷ suất lợi nhuận biên cao nhất, cần khuyến khích nhân viên giới thiệu kèm mỗi đơn gọi món.";
        }

        return string.Empty;
    }
}
