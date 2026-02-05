using ADproject.Controllers;
using ADproject.Models.Entities;
using ADproject.Services;
using ADproject.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;

namespace Pocketree.Api.Tests;

/// <summary>
/// Tests for runtime errors and edge cases (NOT validation errors).
/// Validation errors are tested in AuthenticationTests.cs
/// </summary>
public class ErrorHandlingTests
{
    private DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    #region Task Controller Runtime Errors

    [Fact]
    public async System.Threading.Tasks.Task TaskCompletion_WithNonExistentTask_ReturnsBadRequest()
    {
        // Arrange
        var options = CreateDbOptions("Error_TaskNotFound_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        var mockMlService = Mock.Of<IMlService>();
        var mockHubContext = new Mock<IHubContext<MapHub>>();
        var missionService = new MissionService(context, mockHubContext.Object);
        var controller = new TaskController(context, mockMlService, missionService);
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.Name, "testuser")
        }, "mock"));
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        
        // Act
        var result = await controller.RecordTaskCompletionApi(999, "Completed", null);
        
        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async System.Threading.Tasks.Task SkinRedemption_InsufficientFunds_ReturnsBadRequest()
    {
        // Arrange
        var options = CreateDbOptions("Error_InsufficientFunds_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1,
            TotalCoins = 50, // Not enough!
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
        
        var skin = new Skin
        {
            SkinID = 1,
            SkinName = "Expensive Skin",
            SkinPrice = 500,
            ImageURL = "/images/skins/expensive.png",
            SkinKey = "expensive"
        };
        
        context.Users.Add(user);
        context.Skins.Add(skin);
        await context.SaveChangesAsync();
        
        var mockMlService = Mock.Of<IMlService>();
        var mockHubContext = new Mock<IHubContext<MapHub>>();
        var missionService = new MissionService(context, mockHubContext.Object);
        var controller = new TaskController(context, mockMlService, missionService);
        
        var userClaim = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.Name, "testuser")
        }, "mock"));
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaim }
        };
        
        // Act
        var result = await controller.RedeemSkinApi(1);
        
        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        badRequest.Value.Should().Be("Insufficient coins.");
    }

    #endregion

    #region Database Constraint Errors

    [Fact]
    public async System.Threading.Tasks.Task User_WithInvalidLevelID_DoesNotThrow_InMemory()
    {
        // Arrange
        var options = CreateDbOptions("Error_InvalidLevel_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 999,
            TotalCoins = 0,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
        
        context.Users.Add(user);
        
        // Act & Assert
        var act = async () => await context.SaveChangesAsync();
        await act.Should().NotThrowAsync();
        var saved = await context.Users.FindAsync(1);
        saved.Should().NotBeNull();
        saved!.CurrentLevelID.Should().Be(999);
    }

    #endregion
}
