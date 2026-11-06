using System.Collections.Generic;
using System.Text.Json;

namespace SmartRestaurant.AIEngine;

/// <summary>
/// Helper synthesizing verified database snapshots into grounded prompts to eliminate LLM hallucinations.
/// Formulation: Prompt_final = T_system (+) S_DB(LINQ_Query) (+) Q_user.
/// </summary>
public static class ContextGroundingHelper
{
    public const string NegativeConstraintWarning =
        "QUAN TRỌNG: Bạn là trợ lý ẩm thực và vận hành thông minh của nhà hàng SmartRestaurant. " +
        "Bạn CHỈ ĐƯỢC PHÉP dựa vào dữ liệu thực đơn và số liệu kinh doanh được cung cấp dưới đây để trả lời. " +
        "TUYỆT ĐỐI KHÔNG BỊA ĐẶT món ăn, đơn giá, thành phần hay doanh thu không có trong dữ liệu. " +
        "Nếu khách hàng hỏi về thông tin không có trong danh sách, hãy trả lời lịch sự rằng nhà hàng hiện chưa phục vụ.";

    public static string BuildGroundedSystemPrompt(object snapshotData, string specializedInstruction)
    {
        string json = JsonSerializer.Serialize(snapshotData, new JsonSerializerOptions { WriteIndented = false });
        return $"{NegativeConstraintWarning}\n\n[DỮ LIỆU CSDL NỘI BỘ THỜI GIAN THỰC]:\n{json}\n\n[CHỈ THỊ CHUYÊN BIỆT]:\n{specializedInstruction}";
    }
}
