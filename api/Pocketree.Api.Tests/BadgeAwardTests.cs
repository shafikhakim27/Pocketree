using ADproject.Models.Entities;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pocketree.Api.Tests;

public class BadgeAwardTests
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
                new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250, LevelImageURL = "/images/levels/sapling.png" }
            );
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task Badge_LevelUpToLevel2_AwardsBadge()
    {
        // Arrange
        var options = CreateDbOptions("Badge_Level2_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedRequiredData(context);
        
        var badge = new Badge
        {
            BadgeID = 1,
            BadgeName = "Sapling Achiever",
            Description = "Reached Level 2",
            BadgeImageURL = "badge.png",
            CriteriaType = "LevelUp",
            RequiredCount = 2,
            RequiredDifficulty = "Easy"
        };
        context.Badges.Add(badge);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 250,
            CurrentLevelID = 2,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        // Act
        var eligibleBadges = await context.Badges
            .Where(b => b.CriteriaType == "LevelUp" && b.RequiredCount <= user.CurrentLevelID)
            .ToListAsync();
        
        // Assert
        eligibleBadges.Should().HaveCount(1);
        eligibleBadges[0].BadgeName.Should().Be("Sapling Achiever");
    }

    [Fact]
    public async System.Threading.Tasks.Task Badge_Complete5EasyTasks_AwardsBadge()
    {
        // Arrange
        var options = CreateDbOptions("Badge_EasyTasks");
        using var context = new MyDbContext(options);
        
        var badge = new Badge
        {
            BadgeID = 1,
            BadgeName = "Easy Task Master",
            Description = "Complete 5 Easy tasks",
            BadgeImageURL = "badge.png",
            CriteriaType = "TaskCount",
            RequiredCount = 5,
            RequiredDifficulty = "Easy"
        };
        context.Badges.Add(badge);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 500,
            CurrentLevelID = 2,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        context.Users.Add(user);
        
        var easyTask = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Easy task",
            Difficulty = "Easy",
            CoinReward = 100,
            RequiresEvidence = false,
            Keyword = "test",
            Category = "Testing"
        };
        context.Tasks.Add(easyTask);
        
        // Add 5 completed easy task histories
        for (int i = 1; i <= 5; i++)
        {
            var history = new UserTaskHistory
            {
                HistoryID = i,
                UserID = 1,
                TaskID = 1,
                Status = "Completed",
                CompletionDate = DateTime.UtcNow.AddDays(-i)
            };
            context.UserTaskHistory.Add(history);
        }
        
        await context.SaveChangesAsync();
        
        // Act - Check badge eligibility
        var easyTaskCount = await context.UserTaskHistory
            .CountAsync(th => th.UserID == user.UserID && 
                            th.Task.Difficulty == "Easy" &&
                            th.Status == "Completed");
        
        // Assert
        Assert.Equal(5, easyTaskCount);
        Assert.True(easyTaskCount >= badge.RequiredCount);
    }

    [Fact]
    public async System.Threading.Tasks.Task Badge_UserBadges_DoesNotDuplicate()
    {
        // Arrange
        var options = CreateDbOptions("Badge_NoDuplicate");
        using var context = new MyDbContext(options);
        
        var badge = new Badge
        {
            BadgeID = 1,
            BadgeName = "Test Badge",
            Description = "Test",
            BadgeImageURL = "badge.png",
            CriteriaType = "LevelUp",
            RequiredCount = 1,
            RequiredDifficulty = "Easy"
        };
        context.Badges.Add(badge);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 100,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        context.Users.Add(user);
        
        var userBadge = new UserBadge
        {
            UserBadgeID = 1,
            UserID = 1,
            BadgeID = 1,
            DateEarned = DateTime.UtcNow
        };
        context.UserBadges.Add(userBadge);
        
        await context.SaveChangesAsync();
        
        // Act - Check if user already has this badge
        var currentBadgeIds = await context.UserBadges
            .Where(ub => ub.UserID == user.UserID)
            .Select(ub => ub.BadgeID)
            .ToListAsync();
        
        var availableBadges = await context.Badges
            .Where(b => !currentBadgeIds.Contains(b.BadgeID))
            .ToListAsync();
        
        // Assert
        Assert.Single(currentBadgeIds);
        Assert.Empty(availableBadges); // Badge should not be available again
    }

    [Fact]
    public async System.Threading.Tasks.Task Badge_Level3Achievement_AwardsMightyOakBadge()
    {
        // Arrange
        var options = CreateDbOptions("Badge_MightyOak");
        using var context = new MyDbContext(options);
        
        var badge = new Badge
        {
            BadgeID = 1,
            BadgeName = "Mighty Oak",
            Description = "Reached Level 3",
            BadgeImageURL = "badge.png",
            CriteriaType = "LevelUp",
            RequiredCount = 3,
            RequiredDifficulty = "Easy"
        };
        context.Badges.Add(badge);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 500,
            CurrentLevelID = 3,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        // Act
        var eligibleBadge = await context.Badges
            .FirstOrDefaultAsync(b => b.CriteriaType == "LevelUp" && 
                                     b.RequiredCount <= user.CurrentLevelID);
        
        // Assert
        Assert.NotNull(eligibleBadge);
        Assert.Equal("Mighty Oak", eligibleBadge.BadgeName);
        Assert.Equal(3, eligibleBadge.RequiredCount);
    }

    [Fact]
    public async System.Threading.Tasks.Task Badge_HardTaskMaster_Requires10HardTasks()
    {
        // Arrange
        var options = CreateDbOptions("Badge_HardMaster");
        using var context = new MyDbContext(options);
        
        var badge = new Badge
        {
            BadgeID = 1,
            BadgeName = "Hard Task Champion",
            Description = "Complete 10 Hard tasks",
            BadgeImageURL = "badge.png",
            CriteriaType = "TaskCount",
            RequiredCount = 10,
            RequiredDifficulty = "Hard"
        };
        context.Badges.Add(badge);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 3000,
            CurrentLevelID = 3,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow
        };
        context.Users.Add(user);
        
        var hardTask = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Hard task",
            Difficulty = "Hard",
            CoinReward = 300,
            RequiresEvidence = true,
            Keyword = "recycle",
            Category = "Recycling"
        };
        context.Tasks.Add(hardTask);
        
        // Add 10 completed hard tasks
        for (int i = 1; i <= 10; i++)
        {
            var history = new UserTaskHistory
            {
                HistoryID = i,
                UserID = 1,
                TaskID = 1,
                Status = "Completed",
                CompletionDate = DateTime.UtcNow.AddDays(-i)
            };
            context.UserTaskHistory.Add(history);
        }
        
        await context.SaveChangesAsync();
        
        // Act
        var hardTaskCount = await context.UserTaskHistory
            .CountAsync(th => th.UserID == user.UserID && 
                            th.Task.Difficulty == "Hard" &&
                            th.Status == "Completed");
        
        // Assert
        Assert.Equal(10, hardTaskCount);
        Assert.True(hardTaskCount >= badge.RequiredCount);
    }
}
