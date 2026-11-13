using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartRestaurant.AIEngine;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Interfaces;
using Xunit;

namespace SmartRestaurant.Tests;

public class AiServiceTests
{
    private class MockAiService : IAiService
    {
        public Task<string> GetDishRecommendationAsync(string customerQuery) => Task.FromResult("Mock Recommendation");
        public Task<string> GenerateDishDescriptionAsync(string dishName, List<string> ingredients) => Task.FromResult("Mock Description");
        public Task<string> AnalyzeRevenueTrendsAsync(DateTime startDate, DateTime endDate) => Task.FromResult("Mock Revenue Advice");
        public Task<string> GenerateMenuInsightsAsync() => Task.FromResult("Mock BCG Strategy Advice");
        public Task<string> AnalyzePeakHoursAsync() => Task.FromResult("Mock Peak Hour Advice");
        public Task<string> PredictWeeklyReorderAsync() => Task.FromResult("Mock Reorder Advice");
    }

    private class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception? _exceptionToThrow;

        public ThrowingHttpMessageHandler(Exception? ex = null)
        {
            _exceptionToThrow = ex ?? new HttpRequestException("Connection refused (localhost:1234)");
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw _exceptionToThrow!;
        }
    }

    private class SuccessfulHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;

        public SuccessfulHttpMessageHandler(string content)
        {
            _responseContent = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public string? CapturedRequestBody { get; private set; }
        private readonly string _responseJson;

        public CapturingHttpMessageHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                CapturedRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    [Fact]
    public void GroundingBuilder_InjectsLiveMenuSnapshot_Accurately()
    {
        // Arrange
        var testMenu = new[]
        {
            new { Name = "Phở Bò Tái", Price = "75,000 VND" },
            new { Name = "Bún Chả Hà Nội", Price = "65,000 VND" }
        };
        string instruction = "Tư vấn món ăn truyền thống cho du khách";

        // Act
        string prompt = ContextGroundingHelper.BuildGroundedSystemPrompt(testMenu, instruction);

        // Assert
        Assert.Contains("Phở Bò Tái", prompt);
        Assert.Contains("75,000 VND", prompt);
        Assert.Contains("Bún Chả Hà Nội", prompt);
        Assert.Contains(instruction, prompt);
    }

    [Fact]
    public void ContextGrounding_IncludesNegativeConstraintWarning()
    {
        // Assert
        Assert.Contains("TUYỆT ĐỐI KHÔNG BỊA ĐẶT", ContextGroundingHelper.NegativeConstraintWarning);
        Assert.Contains("CHỈ ĐƯỢC PHÉP dựa vào dữ liệu thực đơn", ContextGroundingHelper.NegativeConstraintWarning);
    }

    [Fact]
    public async Task BcgMatrix_ClassifiesDishesIntoCorrectQuadrants()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var cat = new MenuCategory { CategoryName = "Món Chính" };
        context.MenuCategories.Add(cat);
        await context.SaveChangesAsync();

        var starDish = new MenuItem { Name = "Bò Wagyu", Price = 200000m, CategoryId = cat.CategoryId, IsAvailable = true };
        var plowhorseDish = new MenuItem { Name = "Cơm Trứng", Price = 50000m, CategoryId = cat.CategoryId, IsAvailable = true };
        var puzzleDish = new MenuItem { Name = "Tôm Hùm Đút Lò", Price = 200000m, CategoryId = cat.CategoryId, IsAvailable = true };
        var dogDish = new MenuItem { Name = "Canh Rau Muống", Price = 50000m, CategoryId = cat.CategoryId, IsAvailable = true };

        context.MenuItems.AddRange(starDish, plowhorseDish, puzzleDish, dogDish);
        await context.SaveChangesAsync();

        var order = new Order { TableId = 1, Status = "Completed" };
        order.OrderItems.Add(new OrderItem { MenuItemId = starDish.MenuItemId, Quantity = 10, UnitPrice = 200000m });
        order.OrderItems.Add(new OrderItem { MenuItemId = plowhorseDish.MenuItemId, Quantity = 10, UnitPrice = 50000m });
        order.OrderItems.Add(new OrderItem { MenuItemId = puzzleDish.MenuItemId, Quantity = 1, UnitPrice = 200000m });
        order.OrderItems.Add(new OrderItem { MenuItemId = dogDish.MenuItemId, Quantity = 1, UnitPrice = 50000m });

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var aiEngine = new AiAnalyticsEngine(context, new MockAiService());

        // Act
        var result = await aiEngine.ComputeBcgMatrixAsync();

        // Assert
        Assert.Equal(4, result.Items.Count);
        var star = result.Items.First(i => i.DishName == "Bò Wagyu");
        var plowhorse = result.Items.First(i => i.DishName == "Cơm Trứng");
        var puzzle = result.Items.First(i => i.DishName == "Tôm Hùm Đút Lò");
        var dog = result.Items.First(i => i.DishName == "Canh Rau Muống");

        Assert.Contains("Star", star.Classification);
        Assert.Contains("Plowhorse", plowhorse.Classification);
        Assert.Contains("Puzzle", puzzle.Classification);
        Assert.Contains("Dog", dog.Classification);
    }

    [Fact]
    public async Task ComputePeakHours_CategorizesHoursIntoLevelsCorrectly()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var today = DateTime.UtcNow.Date;

        // Add 10 orders at 12:00 (Rush hour >= 8)
        for (int i = 0; i < 10; i++)
        {
            context.Orders.Add(new Order { TableId = 1, CreatedAt = today.AddHours(12) });
        }
        // Add 5 orders at 15:00 (Medium >= 4)
        for (int i = 0; i < 5; i++)
        {
            context.Orders.Add(new Order { TableId = 1, CreatedAt = today.AddHours(15) });
        }
        // Add 1 order at 10:00 (Low < 4)
        context.Orders.Add(new Order { TableId = 1, CreatedAt = today.AddHours(10) });

        await context.SaveChangesAsync();

        var aiEngine = new AiAnalyticsEngine(context, new MockAiService());

        // Act
        var result = await aiEngine.ComputePeakHoursAsync();

        // Assert
        var rushHour = result.Slots.First(s => s.TimeSlot.StartsWith("12:00"));
        var mediumHour = result.Slots.First(s => s.TimeSlot.StartsWith("15:00"));
        var lowHour = result.Slots.First(s => s.TimeSlot.StartsWith("10:00"));

        Assert.Contains("Cao Điểm", rushHour.BusynessLevel);
        Assert.Contains("Trung Bình", mediumHour.BusynessLevel);
        Assert.Contains("Thấp Điểm", lowHour.BusynessLevel);
    }

    [Fact]
    public async Task ComputeWeeklyReorderForecast_CategorizesUrgencyCorrectly()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var criticalIng = new Ingredient { Name = "Thịt Bò", Unit = "kg", CurrentStock = 1.0m, MinStock = 2.0m }; // <= MinStock
        var warningIng = new Ingredient { Name = "Hành Tây", Unit = "kg", CurrentStock = 2.5m, MinStock = 2.0m };  // <= MinStock * 1.5
        var safeIng = new Ingredient { Name = "Muối", Unit = "kg", CurrentStock = 10.0m, MinStock = 2.0m };       // > MinStock * 1.5

        context.Ingredients.AddRange(criticalIng, warningIng, safeIng);
        await context.SaveChangesAsync();

        var aiEngine = new AiAnalyticsEngine(context, new MockAiService());

        // Act
        var result = await aiEngine.ComputeWeeklyReorderForecastAsync();

        // Assert
        var critical = result.ReorderList.First(i => i.IngredientName == "Thịt Bò");
        var warning = result.ReorderList.First(i => i.IngredientName == "Hành Tây");
        var safe = result.ReorderList.First(i => i.IngredientName == "Muối");

        Assert.Contains("Khẩn Cấp", critical.Urgency);
        Assert.Contains("Cần Lưu Ý", warning.Urgency);
        Assert.Contains("An Toàn", safe.Urgency);
    }

    [Fact]
    public async Task LmStudioService_ReturnsParsedContent_WhenApiRespondsSuccessfully()
    {
        // Arrange
        string mockApiResponse = @"{
            ""id"": ""chatcmpl-123"",
            ""object"": ""chat.completion"",
            ""created"": 1677652288,
            ""choices"": [{
                ""index"": 0,
                ""message"": {
                    ""role"": ""assistant"",
                    ""content"": ""Gợi ý cho bạn món Bò Wagyu A5 thượng hạng kèm vang đỏ.""
                },
                ""finish_reason"": ""stop""
            }]
        }";

        var httpClient = new HttpClient(new SuccessfulHttpMessageHandler(mockApiResponse));
        using var context = TestDbContextFactory.CreateInMemoryContext();
        context.MenuItems.Add(new MenuItem { Name = "Bò Wagyu A5", Price = 450000m, IsAvailable = true, Description = "Ngon" });
        await context.SaveChangesAsync();

        var service = new LmStudioService(httpClient, context);

        // Act
        string reply = await service.GetDishRecommendationAsync("Hôm nay có món gì ngon?");

        // Assert
        Assert.Contains("Bò Wagyu A5", reply);
    }

    [Fact]
    public async Task LmStudioService_HandlesOfflineDaemon_GracefullyWithFallback()
    {
        // Arrange
        var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var lmService = new LmStudioService(httpClient, context);

        // Act
        string response = await lmService.GenerateDishDescriptionAsync("Phở Đặc Biệt", new List<string> { "Bò", "Bánh Phở" });

        // Assert
        Assert.Contains("Chế độ Ngoại tuyến", response);
        Assert.Contains("Máy chủ LM Studio chưa được khởi động", response);
    }

    [Fact]
    public async Task LmStudioService_HandlesTimeout_GracefullyWithFallback()
    {
        // Arrange
        var httpClient = new HttpClient(new ThrowingHttpMessageHandler(new TaskCanceledException("Timed out")));
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var lmService = new LmStudioService(httpClient, context);

        // Act
        string response = await lmService.GetDishRecommendationAsync("Có món gì nhanh?");

        // Assert
        Assert.Contains("Hết thời gian chờ", response);
    }

    [Fact]
    public async Task LmStudioService_AllAnalyticalMethods_ExecuteWithMockApi()
    {
        // Arrange
        string mockApiResponse = @"{
            ""choices"": [{
                ""message"": {
                    ""content"": ""Phân tích thành công từ mô hình AI.""
                }
            }]
        }";

        var httpClient = new HttpClient(new SuccessfulHttpMessageHandler(mockApiResponse));
        using var context = TestDbContextFactory.CreateInMemoryContext();

        var dish = new MenuItem { Name = "Cá Hồi", Price = 300000m, IsAvailable = true };
        context.MenuItems.Add(dish);
        var ing = new Ingredient { Name = "Cá", Unit = "kg", CurrentStock = 5m, MinStock = 2m };
        context.Ingredients.Add(ing);
        var order = new Order { TableId = 1, Status = "Completed", FinalTotal = 300000m, CreatedAt = DateTime.UtcNow };
        order.OrderItems.Add(new OrderItem { MenuItemId = dish.MenuItemId, Quantity = 1, UnitPrice = 300000m });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new LmStudioService(httpClient, context);

        // Act
        var rev = await service.AnalyzeRevenueTrendsAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        var insights = await service.GenerateMenuInsightsAsync();
        var peaks = await service.AnalyzePeakHoursAsync();
        var reorders = await service.PredictWeeklyReorderAsync();

        // Assert
        Assert.Equal("Phân tích thành công từ mô hình AI.", rev);
        Assert.Equal("Phân tích thành công từ mô hình AI.", insights);
        Assert.Equal("Phân tích thành công từ mô hình AI.", peaks);
        Assert.Equal("Phân tích thành công từ mô hình AI.", reorders);
    }

    [Fact]
    public async Task ComputeRevenueInsightsAsync_AggregatesCompletedOrdersCorrectly()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var item1 = new MenuItem { Name = "Cơm Tấm", Price = 70000m, IsAvailable = true };
        var item2 = new MenuItem { Name = "Trà Đá", Price = 10000m, IsAvailable = true };
        context.MenuItems.AddRange(item1, item2);
        await context.SaveChangesAsync();

        var completedOrder = new Order
        {
            TableId = 1,
            Status = "Completed",
            FinalTotal = 80000m,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        completedOrder.OrderItems.Add(new OrderItem { MenuItemId = item1.MenuItemId, Quantity = 1, UnitPrice = 70000m });
        completedOrder.OrderItems.Add(new OrderItem { MenuItemId = item2.MenuItemId, Quantity = 1, UnitPrice = 10000m });

        var pendingOrder = new Order
        {
            TableId = 2,
            Status = "Pending",
            FinalTotal = 70000m,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        pendingOrder.OrderItems.Add(new OrderItem { MenuItemId = item1.MenuItemId, Quantity = 1, UnitPrice = 70000m });

        context.Orders.AddRange(completedOrder, pendingOrder);
        await context.SaveChangesAsync();

        var aiEngine = new AiAnalyticsEngine(context, new MockAiService());

        // Act
        var result = await aiEngine.ComputeRevenueInsightsAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        // Assert
        Assert.Equal(80000m, result.TotalRevenue);
        Assert.Equal(1, result.TotalOrders);
        Assert.NotEmpty(result.TopDishes);
        Assert.Contains("Cơm Tấm", result.TopDishes[0]);
        Assert.Equal("Mock Revenue Advice", result.AiAnalysis);
    }

    [Fact]
    public async Task ComputeWeeklyReorderForecast_ScalesWithRecentBOMConsumption()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var beef = new Ingredient { Name = "Thịt Bò Mỹ", Unit = "kg", CurrentStock = 10m, MinStock = 4m };
        context.Ingredients.Add(beef);
        await context.SaveChangesAsync();

        var pho = new MenuItem { Name = "Phở Bò", Price = 90000m, IsAvailable = true };
        pho.Recipes.Add(new Recipe { IngredientId = beef.IngredientId, QuantityRequired = 1.0m });
        context.MenuItems.Add(pho);
        await context.SaveChangesAsync();

        // 8 orders of pho in the last 7 days -> 8kg consumed -> projected = 10 - 8 = 2kg <= MinStock (4kg)
        var order = new Order { TableId = 1, Status = "Completed", CreatedAt = DateTime.UtcNow.AddDays(-2) };
        order.OrderItems.Add(new OrderItem { MenuItemId = pho.MenuItemId, Quantity = 8, UnitPrice = 90000m });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var aiEngine = new AiAnalyticsEngine(context, new MockAiService());

        // Act
        var result = await aiEngine.ComputeWeeklyReorderForecastAsync();

        // Assert
        var beefItem = result.ReorderList.First(i => i.IngredientName == "Thịt Bò Mỹ");
        Assert.Contains("Cần Lưu Ý", beefItem.Urgency);
        Assert.Contains("8.0 kg/tuần", beefItem.ProjectedUsage);
        Assert.Contains("4.0 kg", beefItem.RecommendedReorder);
    }

    [Fact]
    public async Task LmStudioService_GetDishRecommendation_IncludesRecipeIngredientsInGrounding()
    {
        // Arrange
        string mockApiResponse = @"{
            ""choices"": [{
                ""message"": {
                    ""content"": ""Phở bò rất thơm ngon, an toàn với người ăn thịt bò.""
                }
            }]
        }";
        var handler = new CapturingHttpMessageHandler(mockApiResponse);
        var httpClient = new HttpClient(handler);

        using var context = TestDbContextFactory.CreateInMemoryContext();
        var cat = new MenuCategory { CategoryName = "Món Nước" };
        context.MenuCategories.Add(cat);
        var ingredient = new Ingredient { Name = "Thịt bò tươi", Unit = "kg", CurrentStock = 20, MinStock = 5 };
        context.Ingredients.Add(ingredient);
        await context.SaveChangesAsync();

        var dish = new MenuItem { Name = "Phở Tái", Price = 75000m, IsAvailable = true, CategoryId = cat.CategoryId };
        dish.Recipes.Add(new Recipe { IngredientId = ingredient.IngredientId, QuantityRequired = 0.2m, Ingredient = ingredient });
        context.MenuItems.Add(dish);
        await context.SaveChangesAsync();

        var service = new LmStudioService(httpClient, context);

        // Act
        string reply = await service.GetDishRecommendationAsync("Món phở có thành phần gì?");

        // Assert
        Assert.Contains("Phở bò rất thơm ngon", reply);
        Assert.NotNull(handler.CapturedRequestBody);
        Assert.Contains("Thịt bò tươi", handler.CapturedRequestBody);
    }

    [Fact]
    public async Task LmStudioService_HandlesMalformedAndEmptyResponses()
    {
        // Arrange - Malformed JSON choices
        string badJson = @"{ ""choices"": [] }";
        var clientBad = new HttpClient(new SuccessfulHttpMessageHandler(badJson));
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var serviceBad = new LmStudioService(clientBad, context);

        // Act
        string resultBad = await serviceBad.GenerateDishDescriptionAsync("Bánh Mì", new List<string>());

        // Assert
        Assert.Contains("Định dạng phản hồi từ LM Studio không hợp lệ", resultBad);
    }

    [Fact]
    public async Task LmStudioService_HandlesGenericExceptionGracefully()
    {
        // Arrange
        var client = new HttpClient(new ThrowingHttpMessageHandler(new InvalidOperationException("Fatal socket crash")));
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new LmStudioService(client, context);

        // Act
        string result = await service.GetDishRecommendationAsync("Món ăn");

        // Assert
        Assert.Contains("Lỗi AI", result);
        Assert.Contains("Fatal socket crash", result);
    }

    private class DelegatingAiFailoverHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _geminiResponseJson;
        public List<string> RequestedUrls { get; } = new();

        public DelegatingAiFailoverHttpMessageHandler(string geminiResponseJson)
        {
            _geminiResponseJson = geminiResponseJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri?.ToString() ?? string.Empty;
            RequestedUrls.Add(url);

            if (url.Contains("localhost"))
            {
                throw new HttpRequestException("Connection refused (localhost:1234)");
            }

            if (url.Contains("googleapis.com"))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_geminiResponseJson, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task LmStudioService_FailoverToGemini_WhenLocalServerIsOffline()
    {
        // Arrange
        string geminiMockJson = @"{
            ""candidates"": [
                {
                    ""content"": {
                        ""parts"": [
                            { ""text"": ""Chào quý khách! Tôi gợi ý món Bò Wagyu A5 sốt tiêu đen kèm vang đỏ thượng hạng."" }
                        ],
                        ""role"": ""model""
                    },
                    ""finishReason"": ""STOP""
                }
            ]
        }";

        var handler = new DelegatingAiFailoverHttpMessageHandler(geminiMockJson);
        var httpClient = new HttpClient(handler);
        using var context = TestDbContextFactory.CreateInMemoryContext();

        var service = new LmStudioService(
            httpClient,
            context,
            geminiApiKey: "TEST_GEMINI_KEY",
            geminiModel: "gemini-flash-lite-latest",
            geminiTimeoutSeconds: 5);

        // Act
        string response = await service.GetDishRecommendationAsync("Hôm nay có món gì đặc biệt?");

        // Assert
        Assert.Contains("Google Gemini Backup", response);
        Assert.Contains("gemini-flash-lite-latest", response);
        Assert.Contains("Bò Wagyu A5 sốt tiêu đen", response);
        Assert.Contains(handler.RequestedUrls, u => u.Contains("googleapis.com") && u.Contains("key=TEST_GEMINI_KEY"));
    }

    [Fact]
    public async Task LmStudioService_FallsBackToDeterministicGrounding_WhenBothLocalAndGeminiFail()
    {
        // Arrange
        var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        using var context = TestDbContextFactory.CreateInMemoryContext();

        var service = new LmStudioService(
            httpClient,
            context,
            geminiApiKey: "TEST_GEMINI_KEY",
            geminiModel: "gemini-flash-lite-latest",
            geminiTimeoutSeconds: 5);

        // Act
        string response = await service.GetDishRecommendationAsync("Tôi bị dị ứng hải sản, nên ăn gì?");

        // Assert
        Assert.Contains("CẢNH BÁO AN TOÀN DỊ ỨNG", response);
        Assert.Contains("Tôm Hùm Nướng Phô Mai", response);
        Assert.Contains("Súp Kem Nấm Truffle Hoàng Gia", response);
    }
}
