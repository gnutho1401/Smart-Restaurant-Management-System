using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Service contract for Local GenAI integration via LM Studio offline REST endpoint.
/// </summary>
public interface IAiService
{
    Task<string> GetDishRecommendationAsync(string customerQuery);
    Task<string> GenerateDishDescriptionAsync(string dishName, List<string> ingredients);
    Task<string> AnalyzeRevenueTrendsAsync(DateTime startDate, DateTime endDate);
    Task<string> GenerateMenuInsightsAsync();
    Task<string> AnalyzePeakHoursAsync();
    Task<string> PredictWeeklyReorderAsync();
}
