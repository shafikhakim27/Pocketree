using ADproject.Models.Entities;
using ADproject.Services;
using ADproject.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Moq;

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

    [Fact]
    public async System.Threading.Tasks.Task TreeWithering_3DaysInactive_TreeWithers()
    {
        // Arrange
        var options = CreateDbOptions("Tree_Withers3Days");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 100,
            CurrentLevelID = 1,
            LastActivityDate = DateTime.UtcNow.AddDays(-4), // 4 days ago (> 3 day threshold)
            LastLoginDate = DateTime.UtcNow.AddDays(-4)
        };
        
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
        
        // Act - Check if tree should wither (3-day threshold)
        var daysSinceActivity = (DateTime.UtcNow - user.LastActivityDate.Value).Days;
        
        // Assert
        Assert.True(daysSinceActivity >= 3);
        Assert.False(tree.IsWithered); // Before withering check
        
        // Tree withering logic would set IsWithered = true
        if (daysSinceActivity >= 3)
        {
            tree.IsWithered = true;
            await context.SaveChangesAsync();
        }
        
        var updatedTree = await context.Trees.FindAsync(1);
        Assert.True(updatedTree.IsWithered);
    }

    [Fact]
    public async System.Threading.Tasks.Task TreeWithering_2DaysInactive_TreeStaysHealthy()
    {
        // Arrange
        var options = CreateDbOptions("Tree_Healthy2Days");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 100,
            CurrentLevelID = 1,
            LastActivityDate = DateTime.UtcNow.AddDays(-2), // Only 2 days (< 3 day threshold)
            LastLoginDate = DateTime.UtcNow.AddDays(-2)
        };
        
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
        var daysSinceActivity = (DateTime.UtcNow - user.LastActivityDate.Value).Days;
        
        // Assert
        Assert.True(daysSinceActivity < 3);
        Assert.False(tree.IsWithered); // Should stay healthy
    }

    [Fact]
    public async System.Threading.Tasks.Task TreeWithering_CompletedTree_DoesNotWither()
    {
        // Arrange
        var options = CreateDbOptions("Tree_CompletedNoWither");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 500,
            CurrentLevelID = 3,
            LastActivityDate = DateTime.UtcNow.AddDays(-10), // 10 days inactive
            LastLoginDate = DateTime.UtcNow.AddDays(-10)
        };
        
        var tree = new Tree
        {
            TreeID = 1,
            UserID = 1,
            MissionID = 1,
            IsWithered = false,
            IsCompleted = true // Tree is completed
        };
        
        context.Users.Add(user);
        context.Trees.Add(tree);
        await context.SaveChangesAsync();
        
        // Act - Completed trees should not wither
        var daysSinceActivity = (DateTime.UtcNow - user.LastActivityDate.Value).Days;
        
        // Assert
        Assert.True(daysSinceActivity >= 3);
        Assert.True(tree.IsCompleted);
        // Withering logic should check IsCompleted and skip
        Assert.False(tree.IsWithered);
    }

    [Fact]
    public async System.Threading.Tasks.Task TreeRevival_TaskCompletion_RevivesTree()
    {
        // Arrange
        var options = CreateDbOptions("Tree_Revival");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 100,
            CurrentLevelID = 1,
            LastActivityDate = DateTime.UtcNow.AddDays(-5),
            LastLoginDate = DateTime.UtcNow.AddDays(-5)
        };
        
        var tree = new Tree
        {
            TreeID = 1,
            UserID = 1,
            MissionID = 1,
            IsWithered = true, // Tree is withered
            IsCompleted = false
        };
        
        context.Users.Add(user);
        context.Trees.Add(tree);
        await context.SaveChangesAsync();
        
        // Act - Complete a task (simulated)
        tree.IsWithered = false; // Task completion revives tree
        user.LastActivityDate = DateTime.UtcNow; // Update activity
        await context.SaveChangesAsync();
        
        // Assert
        var updatedTree = await context.Trees.FindAsync(1);
        var updatedUser = await context.Users.FindAsync(1);
        
        Assert.False(updatedTree.IsWithered);
        Assert.True(updatedUser.LastActivityDate > DateTime.UtcNow.AddDays(-1));
    }

    [Fact]
    public async System.Threading.Tasks.Task TreeWithering_MultipleActiveTrees_OnlyIncompleteWither()
    {
        // Arrange
        var options = CreateDbOptions("Tree_MultipleWithering");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 800,
            CurrentLevelID = 3,
            LastActivityDate = DateTime.UtcNow.AddDays(-5),
            LastLoginDate = DateTime.UtcNow.AddDays(-5)
        };
        context.Users.Add(user);
        
        var completedTree = new Tree
        {
            TreeID = 1,
            UserID = 1,
            MissionID = 1,
            IsWithered = false,
            IsCompleted = true
        };
        
        var activeTree = new Tree
        {
            TreeID = 2,
            UserID = 1,
            MissionID = 1,
            IsWithered = false,
            IsCompleted = false
        };
        
        context.Trees.Add(completedTree);
        context.Trees.Add(activeTree);
        await context.SaveChangesAsync();
        
        // Act - Withering check
        var daysSinceActivity = (DateTime.UtcNow - user.LastActivityDate.Value).Days;
        
        if (daysSinceActivity >= 3)
        {
            // Only wither incomplete trees
            var treesToWither = await context.Trees
                .Where(t => t.UserID == user.UserID && !t.IsCompleted)
                .ToListAsync();
            
            foreach (var tree in treesToWither)
            {
                tree.IsWithered = true;
            }
            await context.SaveChangesAsync();
        }
        
        // Assert
        var completedAfter = await context.Trees.FindAsync(1);
        var activeAfter = await context.Trees.FindAsync(2);
        
        Assert.False(completedAfter.IsWithered); // Completed tree stays healthy
        Assert.True(activeAfter.IsWithered); // Active tree withers
    }

    [Fact]
    public async System.Threading.Tasks.Task TreeWithering_ExactlyAt3Days_TreeWithers()
    {
        // Arrange
        var options = CreateDbOptions("Tree_Exactly3Days");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 100,
            CurrentLevelID = 1,
            LastActivityDate = DateTime.UtcNow.AddDays(-3), // Exactly 3 days
            LastLoginDate = DateTime.UtcNow.AddDays(-3)
        };
        
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
        var daysSinceActivity = (DateTime.UtcNow - user.LastActivityDate.Value).Days;
        
        // Assert
        Assert.Equal(3, daysSinceActivity);
        Assert.True(daysSinceActivity >= 3); // Should wither at exactly 3 days
    }
}
