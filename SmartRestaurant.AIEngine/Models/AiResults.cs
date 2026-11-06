using System;
using System.Collections.Generic;

namespace SmartRestaurant.AIEngine.Models;

public record BcgItem(
    string DishName,
    string CategoryName,
    int PortionsSold,
    decimal Revenue,
    string Classification, // Star, Plowhorse, Puzzle, Dog
    string StrategicAction);

public record BcgMatrixResult(
    List<BcgItem> Items,
    string AiExecutiveNarrative,
    DateTime GeneratedAt);

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
    string Urgency); // Critical, Moderate, Normal

public record WeeklyReorderResult(
    List<StockReorderItem> ReorderList,
    string AiLogisticsAdvisory);
