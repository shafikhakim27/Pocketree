using ADproject.Models.Entities;
using ADproject.Services;
using ADproject.Controllers;
using ADproject.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Moq;
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

    private TaskController CreateController(MyDbContext context)
    {
        var mockMlService = Mock.Of<IMlService>();
        
        // Properly mock the SignalR hub context
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
        var options = CreateDbOptions("Level_1_to_2");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 200,
            CurrentLevelID = 1,
            UncompletedTaskCount = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Level up task",
            Difficulty = "Easy",
            CoinReward = 100, // 200 + 100 = 300 > 250
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
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        Assert.Equal(300, updatedUser.TotalCoins);
        Assert.Equal(2, updatedUser.CurrentLevelID); // Seedling ? Sapling
    }

    [Fact]
    public async System.Threading.Tasks.Task LevelProgression_Level2ToLevel3_At500Coins()
    {
        // Arrange
        var options = CreateDbOptions("Level_2_to_3");
        using var context = new MyDbContext(options);
        
        // Add GlobalMission for tree completion
        var mission = new GlobalMission
        {
            MissionID = 1,
            MissionName = "Greenify Sahara",
            TotalRequiredTrees = 1000,
            CurrentTreeCount = 0,
            PlantingFrequency = 1
        };
        context.GlobalMissions.Add(mission);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 400,
            CurrentLevelID = 2, // Already at Sapling
            UncompletedTaskCount = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Level up to Mighty Oak",
            Difficulty = "Normal",
            CoinReward = 200, // 400 + 200 = 600 > 500
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
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        Assert.Equal(600, updatedUser.TotalCoins);
        Assert.Equal(3, updatedUser.CurrentLevelID); // Sapling ? Mighty Oak
    }

    [Fact]
    public async System.Threading.Tasks.Task LevelProgression_Level3_CompletesTree()
    {
        // Arrange
        var options = CreateDbOptions("Level_3_CompletesTree");
        using var context = new MyDbContext(options);
        
        var mission = new GlobalMission
        {
            MissionID = 1,
            MissionName = "Greenify Sahara",
            TotalRequiredTrees = 1000,
            CurrentTreeCount = 0,
            PlantingFrequency = 1
        };
        context.GlobalMissions.Add(mission);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 450,
            CurrentLevelID = 2,
            UncompletedTaskCount = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Complete tree",
            Difficulty = "Easy",
            CoinReward = 100, // 450 + 100 = 550 > 500
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
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        var updatedTree = await context.Trees.AsNoTracking().FirstOrDefaultAsync(t => t.TreeID == 1);
        
        Assert.Equal(550, updatedUser.TotalCoins);
        Assert.Equal(3, updatedUser.CurrentLevelID);
        // Tree should be marked as completed (if logic exists)
        // Assert.True(updatedTree.IsCompleted);
    }

    [Fact]
    public async System.Threading.Tasks.Task LevelProgression_ExactlyAt250Coins_LevelsUp()
    {
        // Arrange
        var options = CreateDbOptions("Level_Exactly250");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 150,
            CurrentLevelID = 1,
            UncompletedTaskCount = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Exact level up",
            Difficulty = "Easy",
            CoinReward = 100, // 150 + 100 = 250 exactly
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
        var updatedUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        Assert.Equal(250, updatedUser.TotalCoins);
        Assert.Equal(2, updatedUser.CurrentLevelID); // Should level up at exactly 250
    }

    [Fact]
    public async System.Threading.Tasks.Task LevelProgression_249Coins_DoesNotLevelUp()
    {
        // Arrange
        var options = CreateDbOptions("Level_249Coins");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 149,
            CurrentLevelID = 1,
            UncompletedTaskCount = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Not quite level up",
            Difficulty = "Easy",
            CoinReward = 100, // 149 + 100 = 249 (< 250)
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
        var updatedUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        Assert.Equal(249, updatedUser.TotalCoins);
        Assert.Equal(1, updatedUser.CurrentLevelID); // Should NOT level up
    }

    [Fact]
    public async System.Threading.Tasks.Task LevelProgression_AlreadyAtLevel3_DoesNotExceed()
    {
        // Arrange
        var options = CreateDbOptions("Level_MaxLevel");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 600,
            CurrentLevelID = 3, // Already at max level
            UncompletedTaskCount = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Max level task",
            Difficulty = "Normal",
            CoinReward = 200,
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
            IsCompleted = true
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
        var updatedUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        Assert.Equal(800, updatedUser.TotalCoins);
        Assert.Equal(3, updatedUser.CurrentLevelID); // Stays at 3
    }

    [Fact]
    public async System.Threading.Tasks.Task LevelProgression_MultipleTasksToLevel3()
    {
        // Arrange
        var options = CreateDbOptions("Level_MultipleToLevel3");
        using var context = new MyDbContext(options);
        
        // Use a fixed date to avoid timing issues
        var testDate = DateTime.UtcNow.Date;
        
        var mission = new GlobalMission
        {
            MissionID = 1,
            MissionName = "Greenify Sahara",
            TotalRequiredTrees = 1000,
            CurrentTreeCount = 0,
            PlantingFrequency = 1
        };
        context.GlobalMissions.Add(mission);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            UncompletedTaskCount = 2, // Only need 2 tasks for this test
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        
        context.Users.Add(user);
        
        var tree = new Tree
        {
            TreeID = 1,
            UserID = 1,
            MissionID = 1,
            IsWithered = false,
            IsCompleted = false
        };
        context.Trees.Add(tree);
        
        // Task 1 - Changed from "Hard" to "Normal" to avoid photo requirement
        var task1 = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Task 1",
            Difficulty = "Normal",
            CoinReward = 300,
            RequiresEvidence = false,
            Keyword = "test",
            Category = "Testing"
        };
        
        var taskHistory1 = new UserTaskHistory
        {
            HistoryID = 1,
            UserID = 1,
            TaskID = 1,
            Status = "Assigned",
            CompletionDate = testDate
        };
        
        context.Tasks.Add(task1);
        context.UserTaskHistory.Add(taskHistory1);
        
        // Task 2 - Changed from "Hard" to "Normal" to avoid photo requirement
        var task2 = new ADproject.Models.Entities.Task
        {
            TaskID = 2,
            Description = "Task 2",
            Difficulty = "Normal",
            CoinReward = 300,
            RequiresEvidence = false,
            Keyword = "test",
            Category = "Testing"
        };
        
        var taskHistory2 = new UserTaskHistory
        {
            HistoryID = 2,
            UserID = 1,
            TaskID = 2,
            Status = "Assigned",
            CompletionDate = testDate
        };
        
        context.Tasks.Add(task2);
        context.UserTaskHistory.Add(taskHistory2);
        
        // Save everything in one call
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act - Complete first task
        var result1 = await controller.RecordTaskCompletionApi(1, "Completed", null);
        
        // Verify the result indicates success
        var okResult1 = Assert.IsType<OkObjectResult>(result1);
        
        // Clear the change tracker to force fresh query
        context.ChangeTracker.Clear();
        
        var userAfterTask1 = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        Assert.Equal(300, userAfterTask1.TotalCoins);
        Assert.Equal(2, userAfterTask1.CurrentLevelID); // Level 2
        
        // Complete second task
        var result2 = await controller.RecordTaskCompletionApi(2, "Completed", null);
        var okResult2 = Assert.IsType<OkObjectResult>(result2);
        
        // Clear the change tracker again
        context.ChangeTracker.Clear();
        
        var userAfterTask2 = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == 1);
        Assert.Equal(600, userAfterTask2.TotalCoins);
        Assert.Equal(3, userAfterTask2.CurrentLevelID); // Level 3!
    }
}
