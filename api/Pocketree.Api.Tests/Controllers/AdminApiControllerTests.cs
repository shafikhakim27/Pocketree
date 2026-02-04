using ADproject.Controllers;
using ADproject.Models.Entities;
using ADproject.Hubs;
using Pocketree.Api.Models.Entities;
using Pocketree.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;

namespace Pocketree.Api.Tests.Controllers;

public class AdminApiControllerTests
{
    private DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private AdminApiController CreateController(MyDbContext context, string? adminId = null)
    {
        var mockHubContext = new Mock<IHubContext<NotificationHub>>();
        var mockPasswordHasher = new Mock<IPasswordHasher<User>>();
        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();

        mockConfig.Setup(c => c["Jwt:Key"]).Returns("TestSecretKey123456789012345678901234567890");
        mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
        mockConfig.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");

        var controller = new AdminApiController(context, mockHubContext.Object, mockPasswordHasher.Object, mockConfig.Object);

        // Mock session with TryGetValue for extension method compatibility
        var sessionData = new Dictionary<string, byte[]>();
        if (!string.IsNullOrEmpty(adminId))
        {
            sessionData["AdminID"] = System.Text.Encoding.UTF8.GetBytes(adminId);
        }
        
        var mockSession = new Mock<ISession>();
        mockSession.Setup(m => m.TryGetValue(It.IsAny<string>(), out It.Ref<byte[]>.IsAny))
            .Returns((string key, out byte[] value) =>
            {
                value = null;
                if (sessionData.TryGetValue(key, out var data))
                {
                    value = data;
                    return true;
                }
                return false;
            });

        var httpContext = new DefaultHttpContext { Session = mockSession.Object };

        // Set admin claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    [Fact]
    public async System.Threading.Tasks.Task FetchAllUsers_ReturnsOnlyPlayers()
    {
        // Arrange
        var options = CreateDbOptions("AdminApi_AllUsers_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });

        context.Users.AddRange(
            new User { UserID = 1, Username = "admin", Email = "admin@test.com", PasswordHash = "hash", ProfileImageURL = "/images/default-user.jpg", CurrentLevelID = 1, TotalCoins = 0, LastLoginDate = DateTime.UtcNow, UserRole = "Admin", IsOnline = false, ResetCode = "", ResetExpiry = default(DateTime), UncompletedTaskCount = 0, NotAttemptedTaskCount = 0, FailedVerificationCount = 0 },
            new User { UserID = 2, Username = "player1", Email = "p1@test.com", PasswordHash = "hash", ProfileImageURL = "/images/default-user.jpg", CurrentLevelID = 1, TotalCoins = 100, LastLoginDate = DateTime.UtcNow, UserRole = "Player", IsOnline = false, ResetCode = "", ResetExpiry = default(DateTime), UncompletedTaskCount = 0, NotAttemptedTaskCount = 0, FailedVerificationCount = 0 },
            new User { UserID = 3, Username = "player2", Email = "p2@test.com", PasswordHash = "hash", ProfileImageURL = "/images/default-user.jpg", CurrentLevelID = 1, TotalCoins = 200, LastLoginDate = DateTime.UtcNow, UserRole = "Player", IsOnline = true, ResetCode = "", ResetExpiry = default(DateTime), UncompletedTaskCount = 0, NotAttemptedTaskCount = 0, FailedVerificationCount = 0 }
        );
        await context.SaveChangesAsync();

        var controller = CreateController(context, adminId: "1");

        // Act
        var result = await controller.FetchAllUsers();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var users = okResult.Value as IEnumerable<object>;
        users.Should().HaveCount(2); // Only players, not admin
    }

    [Fact]
    public async System.Threading.Tasks.Task FetchUsersOnline_ReturnsOnlyOnlinePlayers()
    {
        // Arrange
        var options = CreateDbOptions("AdminApi_OnlineUsers_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });

        context.Users.AddRange(
            new User { UserID = 1, Username = "player1", Email = "p1@test.com", PasswordHash = "hash", ProfileImageURL = "/images/default-user.jpg", CurrentLevelID = 1, TotalCoins = 100, LastLoginDate = DateTime.UtcNow, UserRole = "Player", IsOnline = true, LastActivityDate = DateTime.UtcNow, ResetCode = "", ResetExpiry = default(DateTime), UncompletedTaskCount = 0, NotAttemptedTaskCount = 0, FailedVerificationCount = 0 },
            new User { UserID = 2, Username = "player2", Email = "p2@test.com", PasswordHash = "hash", ProfileImageURL = "/images/default-user.jpg", CurrentLevelID = 1, TotalCoins = 200, LastLoginDate = DateTime.UtcNow, UserRole = "Player", IsOnline = false, LastActivityDate = DateTime.UtcNow, ResetCode = "", ResetExpiry = default(DateTime), UncompletedTaskCount = 0, NotAttemptedTaskCount = 0, FailedVerificationCount = 0 }
        );
        await context.SaveChangesAsync();

        var controller = CreateController(context, adminId: "1");

        // Act
        var result = await controller.FetchUsersOnline();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var users = okResult.Value as IEnumerable<object>;
        users.Should().HaveCount(1);
    }

    [Fact]
    public async System.Threading.Tasks.Task FetchUsersQueries_ReturnsOnlyUsersWithQueries()
    {
        // Arrange
        var options = CreateDbOptions("AdminApi_Queries_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });

        context.Users.AddRange(
            new User { UserID = 1, Username = "player1", Email = "p1@test.com", PasswordHash = "hash", ProfileImageURL = "/images/default-user.jpg", CurrentLevelID = 1, TotalCoins = 100, LastLoginDate = DateTime.UtcNow, UserRole = "Player", IsOnline = false, SupportQuery = "Help with task", ResetCode = "", ResetExpiry = default(DateTime), UncompletedTaskCount = 0, NotAttemptedTaskCount = 0, FailedVerificationCount = 0 },
            new User { UserID = 2, Username = "player2", Email = "p2@test.com", PasswordHash = "hash", ProfileImageURL = "/images/default-user.jpg", CurrentLevelID = 1, TotalCoins = 200, LastLoginDate = DateTime.UtcNow, UserRole = "Player", IsOnline = false, SupportQuery = "", ResetCode = "", ResetExpiry = default(DateTime), UncompletedTaskCount = 0, NotAttemptedTaskCount = 0, FailedVerificationCount = 0 }
        );
        await context.SaveChangesAsync();

        var controller = CreateController(context, adminId: "1");

        // Act
        var result = await controller.FetchUsersQueries();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var queries = okResult.Value as IEnumerable<object>;
        queries.Should().HaveCount(1);
    }

    [Fact]
    public async System.Threading.Tasks.Task ClearUserQueryStatus_ClearsSupportQuery()
    {
        // Arrange
        var options = CreateDbOptions("AdminApi_ClearQuery_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });

        var user = new User
        {
            UserID = 1,
            Username = "player1",
            Email = "p1@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1,
            TotalCoins = 100,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            SupportQuery = "Help needed",
            ResetCode = "",
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CreateController(context, adminId: "1");

        // Act
        var result = await controller.ClearUserQueryStatus(1);

        // Assert
        result.Should().BeOfType<OkResult>();
        var updatedUser = await context.Users.FindAsync(1);
        updatedUser!.SupportQuery.Should().BeNull();
    }

    [Fact]
    public async System.Threading.Tasks.Task ManualPasswordReset_ResetsPassword()
    {
        // Arrange
        var options = CreateDbOptions("AdminApi_PasswordReset_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });

        var user = new User
        {
            UserID = 1,
            Username = "player1",
            Email = "p1@test.com",
            PasswordHash = "oldhash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1,
            TotalCoins = 100,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetCode = "",
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var mockHasher = new Mock<IPasswordHasher<User>>();
        mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), "password")).Returns("newhash");

        var mockHubContext = new Mock<IHubContext<NotificationHub>>();
        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        var controller = new AdminApiController(context, mockHubContext.Object, mockHasher.Object, mockConfig.Object);

        // Act
        var result = await controller.ManualPasswordReset(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var updatedUser = await context.Users.FindAsync(1);
        updatedUser!.PasswordHash.Should().Be("newhash");
    }

    [Fact]
    public async System.Threading.Tasks.Task SendPrivateMessage_WithoutSession_ReturnsUnauthorized()
    {
        // Arrange
        var options = CreateDbOptions("AdminApi_NoSession_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var controller = CreateController(context, adminId: null);

        // Act
        var result = await controller.SendPrivateMessage(1, "Test message");

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async System.Threading.Tasks.Task SendPrivateMessage_WithEmptyMessage_ReturnsBadRequest()
    {
        // Arrange
        var options = CreateDbOptions("AdminApi_EmptyMessage_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var controller = CreateController(context, adminId: "1");

        // Act
        var result = await controller.SendPrivateMessage(1, "   ");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}