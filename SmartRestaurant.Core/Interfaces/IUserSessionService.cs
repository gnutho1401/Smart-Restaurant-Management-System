namespace SmartRestaurant.Core.Interfaces;

public interface IUserSessionService
{
    int CurrentUserId { get; }
    string CurrentUserName { get; }
    string CurrentUserDisplay { get; }
    string Role { get; }

    void SetUser(int userId, string fullName, string role, string? studentId = null);
}
