using ADproject.Models.Entities;
using ADproject.Hubs;
using Pocketree.Api.Controllers;
using Pocketree.Api.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;

namespace Pocketree.Api.Tests.Controllers;

public class AdminControllerTests
{
    private DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private AdminController CreateController(MyDbContext context, int? adminId = null)
    {
        var mockHubContext = new Mock<IHubContext<NotificationHub>>();
        
        // Setup mock to handle SignalR calls
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        
        mockClientProxy
            .Setup(m => m.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(System.Threading.Tasks.Task.CompletedTask);
        
        mockClients
            .Setup(m => m.All)
            .Returns(mockClientProxy.Object);
        
        mockHubContext
            .Setup(m => m.Clients)
            .Returns(mockClients.Object);
        
        var mockPasswordHasher = new Mock<IPasswordHasher<User>>();
        
        var controller = new AdminController(context, mockHubContext.Object, mockPasswordHasher.Object);
        
        var httpContext = new DefaultHttpContext();
        var sessionData = new Dictionary<string, byte[]>();
        
        if (adminId.HasValue)
        {
            // Store AdminID in session as bytes
            sessionData["AdminID"] = System.Text.Encoding.UTF8.GetBytes(adminId.Value.ToString());
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
        
        httpContext.Session = mockSession.Object;
        
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        
        // Setup mock service provider with authentication service
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockAuthService = new Mock<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        mockAuthService
            .Setup(m => m.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<Microsoft.AspNetCore.Authentication.AuthenticationProperties>()))
            .Returns(System.Threading.Tasks.Task.CompletedTask);
        
        mockServiceProvider
            .Setup(m => m.GetService(typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService)))
            .Returns(mockAuthService.Object);
        
        httpContext.RequestServices = mockServiceProvider.Object;
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        controller.Url = new Mock<IUrlHelper>().Object;
        controller.TempData = new TempDataDictionary(httpContext, new Mock<ITempDataProvider>().Object);
        
        return controller;
    }

    [Fact]
    public async System.Threading.Tasks.Task Admin_Index_WithValidSession_ReturnsView()
    {
        // Arrange
        var options = CreateDbOptions("Admin_Index_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });
        context.Users.Add(new User
        {
            UserID = 1,
            Username = "admin",
            Email = "admin@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1,
            TotalCoins = 0,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Admin",
            IsOnline = true,
            ResetCode = "",
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        });
        await context.SaveChangesAsync();
        
        var controller = CreateController(context, adminId: 1);
        
        // Act
        var result = await controller.Index();
        
        // Assert
        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async System.Threading.Tasks.Task Admin_Index_WithoutSession_RedirectsToLogin()
    {
        // Arrange
        var options = CreateDbOptions("Admin_NoSession_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        var controller = CreateController(context, adminId: null);
        
        // Act
        var result = await controller.Index();
        
        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be("Login");
        redirect.ControllerName.Should().Be("User");
    }

    [Fact]
    public async System.Threading.Tasks.Task Admin_Logout_SetsIsOnlineToFalse()
    {
        // Arrange
        var options = CreateDbOptions("Admin_Logout_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });
        context.Users.Add(new User
        {
            UserID = 1,
            Username = "admin",
            Email = "admin@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1,
            TotalCoins = 0,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Admin",
            IsOnline = true,
            ResetCode = "",
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        });
        await context.SaveChangesAsync();
        
        var controller = CreateController(context, adminId: 1);
        
        // Act
        var result = await controller.Logout();
        
        // Assert
        var admin = await context.Users.FindAsync(1);
        admin!.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task Admin_BroadcastMessage_WithEmptyMessage_ReturnsBadRequest()
    {
        // Arrange
        var options = CreateDbOptions("Admin_EmptyBroadcast_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        var controller = CreateController(context, adminId: 1);
        
        // Act
        var result = await controller.BroadcastMessage("   ");
        
        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async System.Threading.Tasks.Task Admin_BroadcastMessage_CreatesNotificationRecord()
    {
        // Arrange
        var options = CreateDbOptions("Admin_Broadcast_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });
        context.Users.Add(new User
        {
            UserID = 1,
            Username = "admin",
            Email = "admin@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1,
            TotalCoins = 0,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Admin",
            IsOnline = true,
            ResetCode = "",
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        });
        await context.SaveChangesAsync();
        
        var controller = CreateController(context, adminId: 1);
        
        // Act
        var result = await controller.BroadcastMessage("Important announcement");
        
        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var notification = await context.NotificationMessages.FirstOrDefaultAsync();
        notification.Should().NotBeNull();
        notification!.Message.Should().Contain("Important announcement");
    }
}
