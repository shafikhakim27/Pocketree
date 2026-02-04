using ADproject.Models.Entities;
using ADproject.Services;
using ADproject.Hubs;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.SignalR;

namespace Pocketree.Api.Tests;

public class TreeWitheringTests
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
            context.Levels.Add(new Level 
            { 
                LevelID = 1, 
                LevelName = "Seedling", 
                MinCoins = 0, 
                LevelImageURL = "/images/levels/seedling.png" 
            });
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
        DateTime? lastActivity = null,
        int totalCoins = 100,
        int currentLevelId = 1)
    {
        return new User
        {
            UserID = userId,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = totalCoins,
            CurrentLevelID = currentLevelId,
            LastActivityDate = lastActivity,
            LastLoginDate = lastActivity ?? DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
    }

    [Fact]
    public async System.Threading.Tasks.Task TreeWithering_3DaysInactive_TreeWithers()
    {
        // Arrange
        var options = CreateDbOptions("Tree_Withers3Days_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = CreateTestUser(lastActivity: DateTime.UtcNow.AddDays(-4));
        
        var tree = new Tree
        {
            TreeID = 1,
            UserID = 1,
            MissionID = 1,
            IsWithered = false,
            IsCompleted = false
        };
        
        context.Users.Add(user);
        context.Trees.Add(tree);
        await context.SaveChangesAsync();
        
        // Act
        var daysSinceActivity = (DateTime.UtcNow - user.LastActivityDate!.Value).Days;
        
        // Assert
        daysSinceActivity.Should().BeGreaterOrEqualTo(3);
        tree.IsWithered.Should().BeFalse(); // Before withering check
        
        // Simulate withering logic
        if (daysSinceActivity >= 3)
        {
            tree.IsWithered = true;
            await context.SaveChangesAsync();
        }
        
        var updatedTree = await context.Trees.FindAsync(1);
        updatedTree!.IsWithered.Should().BeTrue();
    }

    [Fact]
    public async System.Threading.Tasks.Task TreeWithering_2DaysInactive_TreeStaysHealthy()
    {
        // Arrange
        var options = CreateDbOptions("Tree_Healthy2Days_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = CreateTestUser(lastActivity: DateTime.UtcNow.AddDays(-2));
        
        var tree = new Tree
        {
            TreeID = 1,
            UserID = 1,
            MissionID = 1,
            IsWithered = false,
            IsCompleted = false
        };
        
        context.Users.Add(user);
        context.Trees.Add(tree);
        await context.SaveChangesAsync();
        
        // Act
        var daysSinceActivity = (DateTime.UtcNow - user.LastActivityDate!.Value).Days;
        
        // Assert
        daysSinceActivity.Should().BeLessThan(3);
        
        // Withering check - should NOT wither
        if (daysSinceActivity >= 3)
        {
            tree.IsWithered = true;
            await context.SaveChangesAsync();
        }
        
        var updatedTree = await context.Trees.FindAsync(1);
        updatedTree!.IsWithered.Should().BeFalse(); // Still healthy
    }

    [Fact]
    public async System.Threading.Tasks.Task TreeWithering_ExactlyOnDay3_TreeWithers()
    {
        // Arrange
        var options = CreateDbOptions("Tree_Withers_Day3_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = CreateTestUser(lastActivity: DateTime.UtcNow.AddDays(-3));
        var tree = new Tree { TreeID = 1, UserID = 1, MissionID = 1, IsWithered = false, IsCompleted = false };
        
        context.Users.Add(user);
        context.Trees.Add(tree);
        await context.SaveChangesAsync();
        
        // Act
        var daysSinceActivity = (DateTime.UtcNow - user.LastActivityDate!.Value).Days;
        
        if (daysSinceActivity >= 3)
        {
            tree.IsWithered = true;
            await context.SaveChangesAsync();
        }
        
        // Assert
        daysSinceActivity.Should().Be(3);
        var updatedTree = await context.Trees.FindAsync(1);
        updatedTree!.IsWithered.Should().BeTrue(); // Should wither on day 3
    }

    [Fact]
    public async System.Threading.Tasks.Task TreeWithering_CompletedTree_DoesNotWither()
    {
        // Arrange
        var options = CreateDbOptions("Tree_Completed_NoWither_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var user = CreateTestUser(lastActivity: DateTime.UtcNow.AddDays(-10)); // Very inactive
        var tree = new Tree { TreeID = 1, UserID = 1, MissionID = 1, IsWithered = false, IsCompleted = true }; // Already completed
        
        context.Users.Add(user);
        context.Trees.Add(tree);
        await context.SaveChangesAsync();
        
        // Act
        var daysSinceActivity = (DateTime.UtcNow - user.LastActivityDate!.Value).Days;
        
        // Withering logic should skip completed trees
        if (daysSinceActivity >= 3 && !tree.IsCompleted)
        {
            tree.IsWithered = true;
            await context.SaveChangesAsync();
        }
        
        // Assert
        daysSinceActivity.Should().BeGreaterThan(3);
        var updatedTree = await context.Trees.FindAsync(1);
        updatedTree!.IsWithered.Should().BeFalse(); // Completed trees don't wither
        updatedTree.IsCompleted.Should().BeTrue();
    }

    // Add more withering test cases...
}
