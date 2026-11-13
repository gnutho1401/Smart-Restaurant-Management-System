using System.Threading.Tasks;
using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Security;
using SmartRestaurant.Services;
using Xunit;

namespace SmartRestaurant.Tests;

public class AuthServiceTests
{
    [Theory]
    [InlineData("admin123", "q4+PCo11EIoVoDVN+mQ1YQ==:UGIzHYa3oMu8Sm8Cl5yFQl4s62phRYPSNGXhQaknn9E=")]
    [InlineData("manager123", "43QlLzbmZWPrcJrM8yMawg==:5hc7gd11002E8yFg12LuuEV1orxiCtHqOq/ErmrgVFw=")]
    [InlineData("cashier123", "7R4iS7eyCu+dAGMF7FP2pg==:Z4slefUr6yoYAcz9FiymY5YHyuupKvpdgvdcRoVH9Gs=")]
    [InlineData("chef123", "T8VIkLPUJNaqS/qTVqVH+Q==:nO68qiJ0lcfw0nnvnKqjk7M/B6kghaqOd2ikwDCC9lM=")]
    public void VerifySeedHashes(string password, string hash)
    {
        Assert.True(PasswordHasher.VerifyPassword(password, hash));
    }

    [Fact]
    public void PasswordHasher_GeneratesVerifiableHash()
    {
        // Arrange
        string rawPassword = "SecurePassword@123";

        // Act
        string hash = PasswordHasher.HashPassword(rawPassword);
        bool isValid = PasswordHasher.VerifyPassword(rawPassword, hash);

        // Assert
        Assert.NotEmpty(hash);
        Assert.Contains(":", hash);
        Assert.True(isValid);
    }

    [Fact]
    public void PasswordHasher_RejectsWrongPassword()
    {
        // Arrange
        string rawPassword = "CorrectPassword123";
        string wrongPassword = "WrongPassword999";
        string hash = PasswordHasher.HashPassword(rawPassword);

        // Act
        bool isValid = PasswordHasher.VerifyPassword(wrongPassword, hash);

        // Assert
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not_a_valid_hash")]
    [InlineData("singlepart")]
    public void PasswordHasher_HandlesMalformedStoredHash_Gracefully(string? malformedHash)
    {
        // Act
        bool isValid = PasswordHasher.VerifyPassword("AnyPassword", malformedHash!);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsUserAndSetsCurrentSession()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        string password = "adminPassword123";
        var user = new User
        {
            Username = "admin",
            PasswordHash = PasswordHasher.HashPassword(password),
            FullName = "Administrator User",
            Role = UserRole.Admin
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context);

        // Act
        var result = await authService.LoginAsync("admin", password);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("admin", result.Username);
        Assert.Equal(UserRole.Admin, result.Role);
        Assert.True(authService.IsAuthenticated);
        Assert.NotNull(authService.CurrentUser);
        Assert.Equal("admin", authService.CurrentUser.Username);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNullAndDoesNotAuthenticate()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var user = new User
        {
            Username = "cashier_test",
            PasswordHash = PasswordHasher.HashPassword("correct_pass"),
            FullName = "Cashier Test",
            Role = UserRole.Cashier
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context);

        // Act
        var result = await authService.LoginAsync("cashier_test", "incorrect_pass");

        // Assert
        Assert.Null(result);
        Assert.False(authService.IsAuthenticated);
        Assert.Null(authService.CurrentUser);
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var authService = new AuthService(context);

        // Act
        var result = await authService.LoginAsync("unknown_user", "some_password");

        // Assert
        Assert.Null(result);
        Assert.False(authService.IsAuthenticated);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("user", "")]
    [InlineData("   ", "   ")]
    public async Task LoginAsync_WithEmptyCredentials_ReturnsNull(string username, string password)
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var authService = new AuthService(context);

        // Act
        var result = await authService.LoginAsync(username, password);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Logout_ClearsCurrentSessionAndAuthenticationState()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var user = new User
        {
            Username = "manager_test",
            PasswordHash = PasswordHasher.HashPassword("manager123"),
            FullName = "Store Manager",
            Role = UserRole.StoreManager
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context);
        await authService.LoginAsync("manager_test", "manager123");
        Assert.True(authService.IsAuthenticated);

        // Act
        authService.Logout();

        // Assert
        Assert.False(authService.IsAuthenticated);
        Assert.Null(authService.CurrentUser);
    }

    [Fact]
    public async Task HasPermission_AdminEnforcesConfiguredRolesStrictly()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var admin = new User
        {
            Username = "super_admin",
            PasswordHash = PasswordHasher.HashPassword("admin123"),
            Role = UserRole.Admin
        };
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var authService = new AuthService(context);
        await authService.LoginAsync("super_admin", "admin123");

        // Assert: Admin only passes checks where Admin is explicitly allowed
        Assert.True(authService.HasPermission(UserRole.Admin));
        Assert.True(authService.HasPermission(UserRole.Admin, UserRole.StoreManager));
        Assert.False(authService.HasPermission(UserRole.Cashier));
        Assert.False(authService.HasPermission(UserRole.HeadChef));
        Assert.True(authService.HasPermission());
    }

    [Fact]
    public async Task HasPermission_EnforcesRoleBoundariesAccurately()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var cashier = new User
        {
            Username = "cashier_anna",
            PasswordHash = PasswordHasher.HashPassword("123"),
            Role = UserRole.Cashier
        };
        var chef = new User
        {
            Username = "chef_gordon",
            PasswordHash = PasswordHasher.HashPassword("123"),
            Role = UserRole.HeadChef
        };
        context.Users.AddRange(cashier, chef);
        await context.SaveChangesAsync();

        var authService = new AuthService(context);

        // 1. Test Unauthenticated: No permission
        Assert.False(authService.HasPermission(UserRole.Cashier));

        // 2. Test Cashier Permissions
        await authService.LoginAsync("cashier_anna", "123");
        Assert.True(authService.HasPermission(UserRole.Cashier));
        Assert.True(authService.HasPermission(UserRole.Cashier, UserRole.StoreManager));
        Assert.False(authService.HasPermission(UserRole.HeadChef));
        Assert.False(authService.HasPermission(UserRole.StoreManager));

        // 3. Test HeadChef Permissions
        await authService.LoginAsync("chef_gordon", "123");
        Assert.True(authService.HasPermission(UserRole.HeadChef));
        Assert.False(authService.HasPermission(UserRole.Cashier));
        Assert.False(authService.HasPermission(UserRole.StoreManager));
    }
}

