namespace SmartRestaurant.Core.Security;

using SmartRestaurant.Core.Enums;

/// <summary>
/// Strictly defines and enforces the operational hierarchy of restaurant roles:
/// Admin (Rank 4) > StoreManager (Rank 3) > HeadChef (Rank 2) > Cashier (Rank 1).
/// Invariant: Subordinates can NEVER modify, override, or interfere with superiors or peers.
/// </summary>
public static class RoleHierarchy
{
    public static int GetRank(UserRole role) => role switch
    {
        UserRole.Admin => 4,        // Executive System & Business Administrator
        UserRole.StoreManager => 3, // Store Operations General Manager
        UserRole.HeadChef => 2,     // Kitchen & Culinary Department Head
        UserRole.Cashier => 1,      // Front-of-House Point-of-Sale Operator
        _ => 0
    };

    /// <summary>
    /// Checks whether an actor role has strictly higher authority than a target role.
    /// Returns true only if actor's rank is strictly greater than target's rank.
    /// </summary>
    public static bool CanManage(UserRole actorRole, UserRole targetRole)
    {
        return GetRank(actorRole) > GetRank(targetRole);
    }

    /// <summary>
    /// Checks whether an actor role meets or exceeds a required minimum rank.
    /// </summary>
    public static bool MeetsMinimumRank(UserRole actorRole, UserRole requiredMinimumRole)
    {
        return GetRank(actorRole) >= GetRank(requiredMinimumRole);
    }
}

