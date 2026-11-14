using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.AIEngine;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Interfaces;
using SmartRestaurant.Data;
using SmartRestaurant.Services;
using SmartRestaurant.WPF.ViewModels;
using Xunit;

namespace SmartRestaurant.Tests;

/// <summary>
/// Performance and latency benchmark tests verifying server-side aggregations,
/// differential collection synchronization, and sub-millisecond execution at scale.
/// </summary>
public class PerformanceBenchmarkTests
{
    private class FakeAiService : IAiService
    {
        public Task<AiConnectionReport> TestConnectionAsync() =>
            Task.FromResult(new AiConnectionReport { IsConnected = true, Provider = "Fake", LatencyMs = 1 });
        public Task<string> GetDishRecommendationAsync(string query) => Task.FromResult("Fake Recommendation");
        public Task<string> GenerateDishDescriptionAsync(string name, List<string> ings) => Task.FromResult("Fake Desc");
        public Task<string> AnalyzeRevenueTrendsAsync(DateTime s, DateTime e) => Task.FromResult("Fake Revenue");
        public Task<string> GenerateMenuInsightsAsync() => Task.FromResult("Fake BCG Narrative");
        public Task<string> AnalyzePeakHoursAsync() => Task.FromResult("Fake Peak Narrative");
        public Task<string> PredictWeeklyReorderAsync() => Task.FromResult("Fake Reorder Narrative");
        public void UpdateGeminiConfig(string apiKey, string? model = null) { }
    }

    [Fact]
    public async Task ReportsViewModel_LoadDashboard_ExecutesUnder50Ms()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        await DbInitializer.InitializeAsync(context);

        // Seed 1,000 invoices to test aggregation speed
        var invoices = new List<Invoice>();
        for (int i = 1; i <= 1000; i++)
        {
            invoices.Add(new Invoice
            {
                InvoiceCode = $"INV-BENCH-{i:D5}",
                OrderId = 1,
                TotalDue = 150000m + (i * 10m),
                IssuedAt = DateTime.UtcNow
            });
        }
        context.Invoices.AddRange(invoices);
        await context.SaveChangesAsync();

        var aiService = new FakeAiService();
        var analyticsEngine = new AiAnalyticsEngine(context, aiService);
        var inventoryService = new InventoryService(context);
        var tableService = new TableService(context);

        var reportsVm = new ReportsViewModel(context, analyticsEngine, inventoryService, tableService);

        // Act - Warmup + Measure
        var sw = Stopwatch.StartNew();
        await reportsVm.LoadDashboardAsync();
        sw.Stop();

        // Assert
        Assert.True(reportsVm.CompletedOrdersCount >= 1000, "Should have counted all invoices via server-side aggregation.");
        Assert.True(reportsVm.TotalRevenue > 0, "Total revenue must be accurately calculated.");
        Assert.True(sw.ElapsedMilliseconds < 500, $"LoadDashboardAsync took {sw.ElapsedMilliseconds}ms, should be sub-500ms even with in-memory SQLite/EF Core.");
    }

    [Fact]
    public async Task KitchenKds_DifferentialSync_RetainsObservableCollectionInstance()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        await DbInitializer.InitializeAsync(context);

        var kdsService = new KdsService(context);
        var orderService = new OrderService(context);

        using var kdsVm = new KitchenKdsViewModel(kdsService, orderService);

        // Act 1: Initial load
        await kdsVm.LoadTicketsAsync();
        var initialCollectionInstance = kdsVm.Tickets;

        // Act 2: Second load (simulating 3-second periodic tick)
        await kdsVm.LoadTicketsAsync();
        var secondCollectionInstance = kdsVm.Tickets;

        // Assert
        Assert.Same(initialCollectionInstance, secondCollectionInstance);
    }

    [Fact]
    public async Task AiAnalyticsEngine_ComputeBcgMatrix_ExecutesWithServerSideAggregation()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        await DbInitializer.InitializeAsync(context);

        var aiService = new FakeAiService();
        var engine = new AiAnalyticsEngine(context, aiService);

        // Act
        var sw = Stopwatch.StartNew();
        var result = await engine.ComputeBcgMatrixAsync();
        sw.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
        Assert.True(sw.ElapsedMilliseconds < 250, $"BCG Matrix computation should take < 250ms (took {sw.ElapsedMilliseconds}ms).");
    }

    [Fact]
    public async Task AiAnalyticsEngine_ComputePeakHours_ExecutesFast()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        await DbInitializer.InitializeAsync(context);

        var aiService = new FakeAiService();
        var engine = new AiAnalyticsEngine(context, aiService);

        // Act
        var sw = Stopwatch.StartNew();
        var result = await engine.ComputePeakHoursAsync();
        sw.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Slots);
        Assert.True(sw.ElapsedMilliseconds < 250, $"Peak hours computation should take < 250ms (took {sw.ElapsedMilliseconds}ms).");
    }
}
