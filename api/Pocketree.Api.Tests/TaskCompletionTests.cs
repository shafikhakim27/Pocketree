using ADproject.Models.Entities;
using ADproject.Models.DTOs;
using ADproject.Services;
using ADproject.Controllers;
using ADproject.Hubs;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Pocketree.Api.Tests;

public class TaskCompletionTests
{
    private DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private TaskController CreateController(MyDbContext context, IMlService mlService = null)
    {
        var mockMlService = mlService ?? Mock.Of<IMlService>();
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
        
        return controller;
    }

    private async System.Threading.Tasks.Task SeedRequiredData(MyDbContext context)
    {
        if (!context.Levels.Any())
        {
            context.Levels.AddRange(
                new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" },
                new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250, LevelImageURL = "/images/levels/sapling.png" }
            );
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task TaskCompletion_EasyTask_AwardsCorrectCoins()
    {
        // Arrange
        var options = CreateDbOptions("TaskCompletion_EasyTask_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            UncompletedTaskCount = 3,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Easy task",
            Difficulty = "Easy",
            CoinReward = 100,
            RequiresEvidence = false,
            Keyword = "test",
            Category = "Testing"
        };
        
        context.Users.Add(user);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        
        // Create UserTaskHistory record for today (required by RecordTaskCompletionApi)
        var taskHistory = new UserTaskHistory
        {
            UserID = 1,
            TaskID = 1,
            Status = "NotAttempted",
            CompletionDate = DateTime.UtcNow
        };
        context.UserTaskHistory.Add(taskHistory);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RecordTaskCompletionApi(1, "Completed", null);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var resultData = okResult.Value;
        
        var updatedUser = await context.Users.FindAsync(1);
        Assert.Equal(100, updatedUser.TotalCoins);
        Assert.Equal(2, updatedUser.UncompletedTaskCount);
        
        var updatedHistory = await context.UserTaskHistory.FindAsync(1);
        Assert.Equal("Completed", updatedHistory.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task TaskCompletion_NormalTask_AwardsCorrectCoins()
    {
        // Arrange
        var options = CreateDbOptions("TaskCompletion_NormalTask_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            UncompletedTaskCount = 3,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Normal task",
            Difficulty = "Normal",
            CoinReward = 200,
            RequiresEvidence = false,
            Keyword = "test",
            Category = "Testing"
        };
        
        context.Users.Add(user);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        
        // Create UserTaskHistory record for today
        var taskHistory = new UserTaskHistory
        {
            UserID = 1,
            TaskID = 1,
            Status = "NotAttempted",
            CompletionDate = DateTime.UtcNow
        };
        context.UserTaskHistory.Add(taskHistory);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RecordTaskCompletionApi(1, "Completed", null);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedUser = await context.Users.FindAsync(1);
        Assert.Equal(200, updatedUser.TotalCoins);
        Assert.Equal(2, updatedUser.UncompletedTaskCount);
    }

    [Fact]
    public async System.Threading.Tasks.Task TaskCompletion_ReachingLevel2_LevelsUp()
    {
        // Arrange
        var options = CreateDbOptions("TaskCompletion_LevelUp2_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 200, // Already has 200 coins
            CurrentLevelID = 1,
            UncompletedTaskCount = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Task that triggers level up",
            Difficulty = "Easy",
            CoinReward = 100, // This will bring total to 300 (> 250)
            RequiresEvidence = false,
            Keyword = "test",
            Category = "Testing"
        };
        
        context.Users.Add(user);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        
        // Create UserTaskHistory record for today
        var taskHistory = new UserTaskHistory
        {
            UserID = 1,
            TaskID = 1,
            Status = "NotAttempted",
            CompletionDate = DateTime.UtcNow
        };
        context.UserTaskHistory.Add(taskHistory);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RecordTaskCompletionApi(1, "Completed", null);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedUser = await context.Users.FindAsync(1);
        Assert.Equal(300, updatedUser.TotalCoins);
        Assert.Equal(2, updatedUser.CurrentLevelID); // Leveled up to Sapling
    }

    [Fact]
    public async System.Threading.Tasks.Task TaskCompletion_WitheredTree_GetsRevived()
    {
        // Arrange
        var options = CreateDbOptions("TaskCompletion_TreeRevival_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            UncompletedTaskCount = 3,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Task to revive tree",
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
            IsWithered = true, // Tree is withered
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
        var updatedTree = await context.Trees.FindAsync(1);
        Assert.False(updatedTree.IsWithered); // Tree is revived
    }

    [Fact]
    public async System.Threading.Tasks.Task TaskCompletion_PassedStatus_DoesNotAwardCoins()
    {
        // Arrange
        var options = CreateDbOptions("TaskCompletion_Passed_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            UncompletedTaskCount = 3,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Task to pass",
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
        var result = await controller.RecordTaskCompletionApi(1, "Passed", null);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedUser = await context.Users.FindAsync(1);
        Assert.Equal(0, updatedUser.TotalCoins); // No coins awarded for "Passed"
        Assert.Equal(3, updatedUser.UncompletedTaskCount); // Count not decreased
        
        var updatedHistory = await context.UserTaskHistory.FindAsync(1);
        Assert.Equal("Passed", updatedHistory.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task TaskCompletion_UpdatesLastActivityDate()
    {
        // Arrange
        var options = CreateDbOptions("TaskCompletion_ActivityDate_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        
        var oldDate = DateTime.UtcNow.AddDays(-1);
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            UncompletedTaskCount = 1,
            LastLoginDate = oldDate,
            LastActivityDate = oldDate
        };
        
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Activity test task",
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
        var updatedUser = await context.Users.FindAsync(1);
        Assert.NotNull(updatedUser.LastActivityDate);
        Assert.True(updatedUser.LastActivityDate > oldDate);
        Assert.True((DateTime.UtcNow - updatedUser.LastActivityDate.Value).TotalSeconds < 5);
    }

    [Fact]
    public async System.Threading.Tasks.Task TaskCompletion_InvalidUser_ReturnsBadRequest()
    {
        // Arrange
        var options = CreateDbOptions("TaskCompletion_InvalidUser_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RecordTaskCompletionApi(999, "Completed", null);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid User or Task.", badRequestResult.Value);
    }

    [Fact]
    public async System.Threading.Tasks.Task TaskCompletion_InvalidTask_ReturnsBadRequest()
    {
        // Arrange
        var options = CreateDbOptions("TaskCompletion_InvalidTask_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            UncompletedTaskCount = 3,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RecordTaskCompletionApi(999, "Completed", null);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid User or Task.", badRequestResult.Value);
    }
}
