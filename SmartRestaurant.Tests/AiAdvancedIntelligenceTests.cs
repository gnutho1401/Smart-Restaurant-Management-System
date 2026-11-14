using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.AIEngine;
using SmartRestaurant.AIEngine.Models;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;
using SmartRestaurant.Services;
using SmartRestaurant.WPF.ViewModels;
using Xunit;

namespace SmartRestaurant.Tests;

public class AiAdvancedIntelligenceTests
{
    private class DummyAiService : IAiService
    {
        public string? ConfiguredApiKey { get; private set; }
        public string? ConfiguredModel { get; private set; }

        public Task<string> GetDishRecommendationAsync(string customerQuery) => Task.FromResult("Test Recommendation");
        public Task<string> GenerateDishDescriptionAsync(string dishName, List<string> ingredients) => Task.FromResult("Test Description");
        public Task<string> AnalyzeRevenueTrendsAsync(DateTime startDate, DateTime endDate) => Task.FromResult("Test Revenue Analysis");
        public Task<string> GenerateMenuInsightsAsync() => Task.FromResult("Test BCG Insights");
        public Task<string> AnalyzePeakHoursAsync() => Task.FromResult("Test Peak Hour Insights");
        public Task<string> PredictWeeklyReorderAsync() => Task.FromResult("Test Reorder Insights");

        public void UpdateGeminiConfig(string apiKey, string? model = null)
        {
            ConfiguredApiKey = apiKey;
            ConfiguredModel = model;
        }
    }

    [Fact]
    public async Task ComputeBcgMatrix_CalculatesBOMContributionMarginAndPercentages()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var aiService = new DummyAiService();
        var engine = new AiAnalyticsEngine(context, aiService);

        var cat = new MenuCategory { CategoryName = "Món Chính" };
        context.MenuCategories.Add(cat);
        await context.SaveChangesAsync();

        var beef = new Ingredient { IngredientId = 1, Name = "Thịt Bò Wagyu A5", Unit = "g", CurrentStock = 5000m, MinStock = 1000m };
        context.Ingredients.Add(beef);

        var steak = new MenuItem
        {
            MenuItemId = 1,
            CategoryId = cat.CategoryId,
            Name = "Bò Wagyu Nướng",
            Price = 500000m,
            IsAvailable = true
        };
        steak.Recipes.Add(new Recipe { MenuItemId = 1, IngredientId = 1, QuantityRequired = 200m }); // 200g * 1200đ = 240k VND BOM cost
        context.MenuItems.Add(steak);

        // Add 10 sold portions
        var order = new Order { OrderId = 1, Status = "Completed", TableId = 1 };
        order.OrderItems.Add(new OrderItem { OrderId = 1, MenuItemId = 1, Quantity = 10, UnitPrice = 500000m });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        // Act
        var result = await engine.ComputeBcgMatrixAsync();

        // Assert
        Assert.NotEmpty(result.Items);
        var bcgSteak = result.Items.First(i => i.DishName == "Bò Wagyu Nướng");
        Assert.Equal(500000m, bcgSteak.UnitPrice);
        Assert.Equal(240000m, bcgSteak.UnitCost); // 200g * 1,200đ = 240,000đ
        Assert.Equal(260000m, bcgSteak.ContributionMargin); // 500k - 240k = 260k
        Assert.Equal(52m, Math.Round(bcgSteak.MarginPercentage)); // 260k / 500k = 52%
        Assert.Equal(10, bcgSteak.PortionsSold);
        Assert.Contains("Star", bcgSteak.Classification); // High volume and high margin
    }

    [Fact]
    public async Task ComputeBasketAssociations_Apriori_IdentifiesFrequentItemPairsAndLift()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var aiService = new DummyAiService();
        var engine = new AiAnalyticsEngine(context, aiService);

        var steak = new MenuItem { MenuItemId = 1, Name = "Bò Wagyu Áp Chảo", Price = 450000m, IsAvailable = true };
        var wine = new MenuItem { MenuItemId = 2, Name = "Rượu Vang Đỏ", Price = 150000m, IsAvailable = true };
        var salad = new MenuItem { MenuItemId = 3, Name = "Salad Cá Hồi", Price = 120000m, IsAvailable = true };
        context.MenuItems.AddRange(steak, wine, salad);

        // Seed 4 orders with (Steak + Wine), 1 with (Steak + Salad)
        for (int i = 1; i <= 4; i++)
        {
            var ord = new Order { OrderId = i, Status = "Completed", TableId = 1 };
            ord.OrderItems.Add(new OrderItem { OrderId = i, MenuItemId = 1, MenuItem = steak, Quantity = 1, UnitPrice = 450000m });
            ord.OrderItems.Add(new OrderItem { OrderId = i, MenuItemId = 2, MenuItem = wine, Quantity = 1, UnitPrice = 150000m });
            context.Orders.Add(ord);
        }

        var ord5 = new Order { OrderId = 5, Status = "Completed", TableId = 2 };
        ord5.OrderItems.Add(new OrderItem { OrderId = 5, MenuItemId = 1, MenuItem = steak, Quantity = 1, UnitPrice = 450000m });
        ord5.OrderItems.Add(new OrderItem { OrderId = 5, MenuItemId = 3, MenuItem = salad, Quantity = 1, UnitPrice = 120000m });
        context.Orders.Add(ord5);
        await context.SaveChangesAsync();

        // Act
        var rules = await engine.ComputeBasketAssociationsAsync();

        // Assert
        Assert.NotEmpty(rules);
        var steakToWine = rules.FirstOrDefault(r => r.PrimaryDishId == 1 && r.SuggestedDishId == 2);
        Assert.NotNull(steakToWine);
        Assert.Equal(0.8, Math.Round(steakToWine.Confidence, 2)); // 4 out of 5 steak orders included wine = 80%
        Assert.True(steakToWine.Lift >= 1.0);
        Assert.Contains("Rượu Vang Đỏ", steakToWine.MarketingPitch);
    }

    [Fact]
    public async Task GetSmartUpsellRecommendation_ReturnsHighLiftRuleOrCulinaryFallback()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var aiService = new DummyAiService();
        var engine = new AiAnalyticsEngine(context, aiService);

        var catFood = new MenuCategory { CategoryId = 1, CategoryName = "Món Chính" };
        var catDrink = new MenuCategory { CategoryId = 2, CategoryName = "Đồ Uống (Beverages)" };
        context.MenuCategories.AddRange(catFood, catDrink);
        await context.SaveChangesAsync();

        var food = new MenuItem { MenuItemId = 1, CategoryId = 1, Name = "Lẩu Bò Wagyu", Price = 380000m, IsAvailable = true };
        var drink = new MenuItem { MenuItemId = 2, CategoryId = 2, Name = "Trà Đào Cam Sả", Price = 45000m, IsAvailable = true };
        context.MenuItems.AddRange(food, drink);
        await context.SaveChangesAsync();

        // Act - Request upsell when food is selected and cart currently only has food (ID 1)
        var recommendation = await engine.GetSmartUpsellRecommendationAsync(1, new List<int> { 1 });

        // Assert
        Assert.NotNull(recommendation);
        Assert.Equal(2, recommendation.SuggestedDishId);
        Assert.Equal("Trà Đào Cam Sả", recommendation.SuggestedDishName);
        Assert.Contains("Trà Đào Cam Sả", recommendation.MarketingPitch);
    }

    [Fact]
    public async Task ComputeWeeklyReorderForecast_GeneratesValidPurchaseOrderDraft()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var aiService = new DummyAiService();
        var engine = new AiAnalyticsEngine(context, aiService);

        // Ingredient below MinStock -> Needs PO replenishment
        var criticalIng = new Ingredient
        {
            IngredientId = 10,
            Name = "Nấm Truffle Đen",
            Unit = "g",
            CurrentStock = 100m,
            MinStock = 500m
        };
        context.Ingredients.Add(criticalIng);
        await context.SaveChangesAsync();

        // Act
        var result = await engine.ComputeWeeklyReorderForecastAsync();

        // Assert
        Assert.NotEmpty(result.ReorderList);
        Assert.NotNull(result.PurchaseOrderDraft);
        Assert.NotEmpty(result.PurchaseOrderDraft);

        var draft = result.PurchaseOrderDraft.First(p => p.IngredientId == 10);
        Assert.Equal("Nấm Truffle Đen", draft.IngredientName);
        Assert.True(draft.QuantityToOrder >= 400m); // Needs at least to replenish to MinStock
        Assert.Contains("Khẩn Cấp", draft.Urgency);
    }

    [Fact]
    public async Task ApplyPurchaseOrderDraft_RestocksIngredientsAndLogsAuditTrail()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var auditService = new AuditService(context);
        var inventoryService = new InventoryService(context, auditService);

        var ing = new Ingredient
        {
            IngredientId = 5,
            Name = "Tôm Hùm Alaska",
            Unit = "g",
            CurrentStock = 500m,
            MinStock = 2000m
        };
        context.Ingredients.Add(ing);
        await context.SaveChangesAsync();

        var poList = new List<PurchaseOrderDraftItem>
        {
            new(5, "Tôm Hùm Alaska", 1500m, "g", "Khẩn Cấp", "Bù đắp thiếu hụt tồn kho")
        };

        // Act
        int replenished = await inventoryService.ApplyPurchaseOrderDraftAsync(poList);

        // Assert
        Assert.Equal(1, replenished);
        var updatedIng = await context.Ingredients.FindAsync(5);
        Assert.NotNull(updatedIng);
        Assert.Equal(2000m, updatedIng.CurrentStock); // 500 + 1500 = 2000g

        // Verify Audit Trail recorded the restock event
        var auditLogs = await context.AuditLogs.Where(a => a.Action == "RESTOCK_AI_PO").ToListAsync();
        Assert.Single(auditLogs);
        Assert.Contains("Tự động nhập kho từ Đề xuất AI PO", auditLogs[0].Details);
        Assert.Equal("5", auditLogs[0].EntityId);
    }

    [Fact]
    public void AiChatViewModel_SemanticCache_And_SettingsUpdate()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var aiService = new DummyAiService();
        var engine = new AiAnalyticsEngine(context, aiService);
        var vm = new AiChatViewModel(aiService, engine);

        // Act - Update Gemini API Settings
        vm.GeminiApiKeyInput = "AIzaSyTestKey123456";
        vm.SelectedGeminiModel = "gemini-1.5-flash";
        vm.SaveAiSettingsCommand.Execute(null);

        // Assert
        Assert.Equal("AIzaSyTestKey123456", aiService.ConfiguredApiKey);
        Assert.Equal("gemini-1.5-flash", aiService.ConfiguredModel);
        Assert.Contains("Đã lưu cấu hình", vm.SettingsFeedback);
    }
}
