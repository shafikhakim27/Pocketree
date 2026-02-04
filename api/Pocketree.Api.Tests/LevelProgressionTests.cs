using ADproject.Models.Entities;
using ADproject.Services;
using ADproject.Controllers;
using ADproject.Hubs;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Pocketree.Api.Tests;

public class LevelProgressionTests
{
    private DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private async System.Threading.Tasks.Task SeedRequiredData(MyDbContext context)
    {
        if (!context.Levels.Any())
        {
            context.Levels.AddRange(
                new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" },
                new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250, LevelImageURL = "/images/levels/sapling.png" },
                new Level { LevelID = 3, LevelName = "Mighty Oak", MinCoins = 500, LevelImageURL = "/images/levels/oak.png" }
            );
            await context.SaveChangesAsync();
        }

        if (!context.GlobalMissions.Any())
        {
            context.GlobalMissions.Add(new GlobalMission
            {
                MissionID = 1,
                MissionName = "Greenify Sahara",
                TotalRequiredTrees = 1000,
                CurrentTreeCount = 0,
                PlantingFrequency = 1
            });
            await context.SaveChangesAsync();
        }
    }

    private User CreateTestUser(
        int userId = 1,
        string username = "testuser",
        int totalCoins = 0,
        int currentLevelId = 1,
        int uncompletedTaskCount = 1,
        DateTime? lastActivity = null)
    {
        return new User
        {
            UserID = userId,
            Username = username,
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = totalCoins,
            CurrentLevelID = currentLevelId,
            UncompletedTaskCount = uncompletedTaskCount,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = lastActivity ?? DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
    }

    private TaskController CreateController(MyDbContext context)
    {
        var mockMlService = Mock.Of<IMlService>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(clients => clients.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        
        var mockHubContext = new Mock<IHubContext<MapHub>>();
        mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        
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
        
        return controller;
    }

    [Fact]
    public async System.Threading.Tasks.Task LevelProgression_Level1ToLevel2_At250Coins()
    {
        // Arrange
        var options = CreateDbOptions("Level_1_to_2_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = CreateTestUser(totalCoins: 200);
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Level up task",
            Difficulty = "Easy",
            CoinReward = 100,
            RequiresEvidence = false,
            Keyword = "test",
            Category = "Testing"
        };
        
        var tree = new Tree
        {
            TreeID = 1,
            UserID = 1,
            MissionID = 1,
            IsWithered = false,
            IsCompleted = false
        };
        
        var taskHistory = new UserTaskHistory
        {
            HistoryID = 1,
            UserID = 1,
            TaskID = 1,
            Status = "Assigned",
            CompletionDate = DateTime.UtcNow.Date
        };
        
        context.Users.Add(user);
        context.Tasks.Add(task);
        context.Trees.Add(tree);
        context.UserTaskHistory.Add(taskHistory);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RecordTaskCompletionApi(1, "Completed", null);
        
        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var updatedUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        updatedUser.TotalCoins.Should().Be(300);
        updatedUser.CurrentLevelID.Should().Be(2); // Leveled up!
    }

    [Fact]
    public async System.Threading.Tasks.Task LevelProgression_Level2ToLevel3_At500Coins()
    {
        // Arrange
        var options = CreateDbOptions("Level_2_to_3_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = CreateTestUser(totalCoins: 400, currentLevelId: 2);
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Level up to Mighty Oak",
            Difficulty = "Normal",
            CoinReward = 200,
            RequiresEvidence = false,
            Keyword = "test",
            Category = "Testing"
        };
        
        var tree = new Tree { TreeID = 1, UserID = 1, MissionID = 1, IsWithered = false, IsCompleted = false };
        var taskHistory = new UserTaskHistory { HistoryID = 1, UserID = 1, TaskID = 1, Status = "Assigned", CompletionDate = DateTime.UtcNow.Date };
        
        context.Users.Add(user);
        context.Tasks.Add(task);
        context.Trees.Add(tree);
        context.UserTaskHistory.Add(taskHistory);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RecordTaskCompletionApi(1, "Completed", null);
        
        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var updatedUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        updatedUser.TotalCoins.Should().Be(600);
        updatedUser.CurrentLevelID.Should().Be(3); // Sapling → Mighty Oak
    }

    [Fact]
    public async System.Threading.Tasks.Task LevelProgression_ExactlyAt250Coins_LevelsUp()
    {
        // Arrange
        var options = CreateDbOptions("Level_Exactly250_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = CreateTestUser(totalCoins: 150);
        var task = new ADproject.Models.Entities.Task { TaskID = 1, Description = "Exact level up", Difficulty = "Easy", CoinReward = 100, RequiresEvidence = false, Keyword = "test", Category = "Testing" };
        var tree = new Tree { TreeID = 1, UserID = 1, MissionID = 1, IsWithered = false, IsCompleted = false };
        var taskHistory = new UserTaskHistory { HistoryID = 1, UserID = 1, TaskID = 1, Status = "Assigned", CompletionDate = DateTime.UtcNow.Date };
        
        context.Users.Add(user);
        context.Tasks.Add(task);
        context.Trees.Add(tree);
        context.UserTaskHistory.Add(taskHistory);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RecordTaskCompletionApi(1, "Completed", null);
        
        // Assert
        var updatedUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        updatedUser.TotalCoins.Should().Be(250);
        updatedUser.CurrentLevelID.Should().Be(2); // Should level up at exactly 250
    }

    [Fact]
    public async System.Threading.Tasks.Task LevelProgression_249Coins_DoesNotLevelUp()
    {
        // Arrange
        var options = CreateDbOptions("Level_249Coins_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = CreateTestUser(totalCoins: 149);
        var task = new ADproject.Models.Entities.Task { TaskID = 1, Description = "Not quite level up", Difficulty = "Easy", CoinReward = 100, RequiresEvidence = false, Keyword = "test", Category = "Testing" };
        var tree = new Tree { TreeID = 1, UserID = 1, MissionID = 1, IsWithered = false, IsCompleted = false };
        var taskHistory = new UserTaskHistory { HistoryID = 1, UserID = 1, TaskID = 1, Status = "Assigned", CompletionDate = DateTime.UtcNow.Date };
        
        context.Users.Add(user);
        context.Tasks.Add(task);
        context.Trees.Add(tree);
        context.UserTaskHistory.Add(taskHistory);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RecordTaskCompletionApi(1, "Completed", null);
        
        // Assert
        var updatedUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        updatedUser.TotalCoins.Should().Be(249);
        updatedUser.CurrentLevelID.Should().Be(1); // Should NOT level up
    }

    [Fact]
    public async System.Threading.Tasks.Task LevelProgression_AlreadyAtLevel3_DoesNotExceed()
    {
        // Arrange
        var options = CreateDbOptions("Level_MaxLevel_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = CreateTestUser(totalCoins: 600, currentLevelId: 3);
        var task = new ADproject.Models.Entities.Task { TaskID = 1, Description = "Max level task", Difficulty = "Normal", CoinReward = 200, RequiresEvidence = false, Keyword = "test", Category = "Testing" };
        var tree = new Tree { TreeID = 1, UserID = 1, MissionID = 1, IsWithered = false, IsCompleted = true };
        var taskHistory = new UserTaskHistory { HistoryID = 1, UserID = 1, TaskID = 1, Status = "Assigned", CompletionDate = DateTime.UtcNow.Date };
        
        context.Users.Add(user);
        context.Tasks.Add(task);
        context.Trees.Add(tree);
        context.UserTaskHistory.Add(taskHistory);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RecordTaskCompletionApi(1, "Completed", null);
        
        // Assert
        var updatedUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        updatedUser.TotalCoins.Should().Be(800);
        updatedUser.CurrentLevelID.Should().Be(3); // Stays at max level
    }

    // Add remaining tests following the same pattern...
}
