namespace SmartRestaurant.Core.Enums;

/// <summary>
/// Loyalty membership tiers for recurring customers based on accumulated points and spending.
/// </summary>
public enum MembershipTier
{
    Standard, // Đồng / Tiêu Chuẩn (0-99 điểm)
    Silver,   // Bạc (100-499 điểm) - Giảm 5%
    Gold,     // Vàng (500-999 điểm) - Giảm 10%
    Diamond   // Kim Cương (1000+ điểm) - Giảm 15%
}
