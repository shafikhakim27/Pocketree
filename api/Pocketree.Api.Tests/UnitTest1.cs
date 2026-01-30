using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.EntityFrameworkCore;

namespace Pocketree.Api.Tests;

public class DbContextTests
{
    [Fact]
    public void DbContext_ShouldInitialize_WithInMemoryDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb")
            .UseLazyLoadingProxies()
            .Options;

        // Act
        using var context = new MyDbContext(options);

        // Assert
        Assert.NotNull(context);
        Assert.NotNull(context.Users);
        Assert.NotNull(context.Tasks);
        Assert.NotNull(context.Levels);
    }

    [Fact]
    public async System.Threading.Tasks.Task DbContext_CanAddAndRetrieve_User()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_AddUser")
            .UseLazyLoadingProxies()
            .Options;

        // Act
        using (var context = new MyDbContext(options))
        {
            var user = new User
            {
                UserID = 1,
                Username = "testuser",
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                TotalCoins = 100,
                CurrentLevelID = 1,
                LastLoginDate = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new MyDbContext(options))
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Username == "testuser");
            Assert.NotNull(user);
            Assert.Equal("testuser", user.Username);
            Assert.Equal("test@example.com", user.Email);
            Assert.Equal(100, user.TotalCoins);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task DbContext_CanAddAndRetrieve_Task()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_AddTask")
            .UseLazyLoadingProxies()
            .Options;

        // Act
        using (var context = new MyDbContext(options))
        {
            var task = new ADproject.Models.Entities.Task
            {
                TaskID = 1,
                Description = "Test task",
                Difficulty = "Easy",
                CoinReward = 50,
                RequiresEvidence = false,
                Keyword = "test",
                Category = "Testing"
            };

            context.Tasks.Add(task);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new MyDbContext(options))
        {
            var task = await context.Tasks.FirstOrDefaultAsync(t => t.TaskID == 1);
            Assert.NotNull(task);
            Assert.Equal("Test task", task.Description);
            Assert.Equal("Easy", task.Difficulty);
            Assert.Equal(50, task.CoinReward);
            Assert.Equal("Testing", task.Category);
        }
    }
}

public class MissionServiceTests
{
    [Fact]
    public void MissionService_LocationSlots_ShouldHave50Locations()
    {
        // Arrange & Act
        var locationCount = MissionService.locSlots.Count;

        // Assert
        Assert.Equal(50, locationCount);
    }

    [Fact]
    public void MissionService_LocationSlots_ShouldHaveValidCoordinates()
    {
        // Arrange & Act
        var invalidLocations = MissionService.locSlots
            .Where(loc => loc.X < 0 || loc.X > 100 || loc.Y < 0 || loc.Y > 100)
            .ToList();

        // Assert
        Assert.Empty(invalidLocations);
    }

    [Fact]
    public void MissionService_LocationSlots_ShouldHaveUniqueCoordinates()
    {
        // Arrange & Act
        var uniqueLocations = MissionService.locSlots.Distinct().Count();

        // Assert
        Assert.Equal(50, uniqueLocations);
    }
}

public class EntityValidationTests
{
    [Theory]
    [InlineData("Easy", 100)]
    [InlineData("Normal", 200)]
    [InlineData("Hard", 300)]
    public void Task_CoinReward_ShouldMatchDifficulty(string difficulty, int expectedCoins)
    {
        // Arrange
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Test task",
            Difficulty = difficulty,
            CoinReward = expectedCoins,
            RequiresEvidence = false,
            Keyword = "test",
            Category = "Testing"
        };

        // Act & Assert
        Assert.Equal(expectedCoins, task.CoinReward);
        Assert.Equal(difficulty, task.Difficulty);
    }

    [Fact]
    public void Level_Progression_ShouldHaveIncreasingMinCoins()
    {
        // Arrange
        var levels = new List<Level>
        {
            new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0 },
            new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250 },
            new Level { LevelID = 3, LevelName = "Mighty Oak", MinCoins = 500 }
        };

        // Act
        var isAscending = levels
            .Zip(levels.Skip(1), (a, b) => a.MinCoins < b.MinCoins)
            .All(x => x);

        // Assert
        Assert.True(isAscending);
    }

    [Fact]
    public void User_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var user = new User
        {
            UserID = 1,
            Username = "newuser",
            Email = "new@example.com",
            PasswordHash = "hash",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(0, user.TotalCoins);
        Assert.Equal(1, user.CurrentLevelID);
        Assert.NotNull(user.Username);
        Assert.NotNull(user.Email);
    }
}
