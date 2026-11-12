using SmartRestaurant.Core.Interfaces;

namespace SmartRestaurant.Services;

public class UserSessionService : IUserSessionService
{
    public int CurrentUserId { get; private set; } = 1;
    public string CurrentUserName { get; private set; } = "Hồ Thăng Tùng";
    public string CurrentUserDisplay { get; private set; } = "Hồ Thăng Tùng (Trưởng Nhóm - DE190479)";
    public string Role { get; private set; } = "Admin";

    public void SetUser(int userId, string fullName, string role, string? studentId = null)
    {
        CurrentUserId = userId;
        CurrentUserName = fullName;
        Role = role;
        CurrentUserDisplay = string.IsNullOrWhiteSpace(studentId)
            ? $"{fullName} ({role})"
            : $"{fullName} ({role} - {studentId})";
    }
}
