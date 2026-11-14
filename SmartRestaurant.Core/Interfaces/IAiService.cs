using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartRestaurant.Core.Interfaces;

/// <summary>
/// Diagnostic report for AI service connectivity (LM Studio Local vs Google Gemini Cloud).
/// </summary>
public class AiConnectionReport
{
    public bool IsConnected { get; set; }
    public string Provider { get; set; } = "None"; // "LM Studio (Cục bộ)", "Google Gemini (Đám mây)", "Offline Grounding"
    public string Endpoint { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
    public string Message { get; set; } = string.Empty;
}

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
    void UpdateGeminiConfig(string apiKey, string? model = null);
    Task<AiConnectionReport> TestConnectionAsync();
    string GetLastUsedProvider();
}
