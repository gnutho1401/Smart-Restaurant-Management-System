using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection refused (localhost:1234)");
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

        // 4 dishes with varied price:
        // Average price will be (200 + 50 + 200 + 50) / 4 = 125k
        // Star: high price (200k), high volume (10 sold)
        // Plowhorse: low price (50k), high volume (10 sold)
        // Puzzle: high price (200k), low volume (1 sold)
        // Dog: low price (50k), low volume (1 sold)
        var starDish = new MenuItem { Name = "Bò Wagyu", Price = 200000m, CategoryId = cat.CategoryId, IsAvailable = true };
        var plowhorseDish = new MenuItem { Name = "Cơm Trứng", Price = 50000m, CategoryId = cat.CategoryId, IsAvailable = true };
        var puzzleDish = new MenuItem { Name = "Tôm Hùm Đút Lò", Price = 200000m, CategoryId = cat.CategoryId, IsAvailable = true };
        var dogDish = new MenuItem { Name = "Canh Rau Muống", Price = 50000m, CategoryId = cat.CategoryId, IsAvailable = true };

        context.MenuItems.AddRange(starDish, plowhorseDish, puzzleDish, dogDish);
        await context.SaveChangesAsync();

        // Orders: Average sold will be (10 + 10 + 1 + 1) / 4 = 5.5
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
    public async Task AiClient_HandlesOfflineDaemon_GracefullyWithFallback()
    {
        // Arrange: HttpClient configured with failing message handler
        var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var lmService = new LmStudioService(httpClient, context);

        // Act
        string response = await lmService.GenerateDishDescriptionAsync("Phở Đặc Biệt", new List<string> { "Bò", "Bánh Phở" });

        // Assert: Returns offline fallback message instead of throwing an unhandled crash
        Assert.Contains("Chế độ Ngoại tuyến", response);
        Assert.Contains("Máy chủ LM Studio chưa được khởi động", response);
    }
}
