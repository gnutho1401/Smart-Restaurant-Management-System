using System;
using System.Collections.Generic;
using SmartRestaurant.Core.Entities;

namespace SmartRestaurant.AIEngine.Models;

public record BcgItem(
    string DishName,
    string CategoryName,
    int PortionsSold,
    decimal Revenue,
    string Classification, // Star, Plowhorse, Puzzle, Dog
    string StrategicAction,
    decimal UnitPrice = 0m,
    decimal UnitCost = 0m,
    decimal ContributionMargin = 0m,
    decimal MarginPercentage = 0m);

public record BcgMatrixResult(
    List<BcgItem> Items,
    string AiExecutiveNarrative,
    DateTime GeneratedAt,
    decimal AverageMargin = 0m,
    decimal AverageVolume = 0m);

public record RevenueInsightResult(
    string Period,
    decimal TotalRevenue,
    int TotalOrders,
    List<string> TopDishes,
    string AiAnalysis);

public record PeakHourSlot(
    string TimeSlot,
    int OrderVolume,
    string BusynessLevel);

public record PeakHourResult(
    List<PeakHourSlot> Slots,
    string PrimaryPeakPeriod,
    string StaffingRecommendations);

public record StockReorderItem(
    string IngredientName,
    string CurrentStock,
    string MinStock,
    string Urgency,
    string ProjectedUsage = "",
    string RecommendedReorder = "",
    int IngredientId = 0,
    decimal RawReorderQty = 0m,
    string Unit = "g");

public record WeeklyReorderResult(
    List<StockReorderItem> ReorderList,
    string AiLogisticsAdvisory,
    List<PurchaseOrderDraftItem> PurchaseOrderDraft = null!);

public record BasketAssociationRule(
    int PrimaryDishId,
    string PrimaryDishName,
    int SuggestedDishId,
    string SuggestedDishName,
    decimal SuggestedDishPrice,
    double Support,
    double Confidence,
    double Lift,
    string MarketingPitch);
