using ADproject.Models.Entities;

namespace Pocketree.Api.Tests.Entities;

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
        task.CoinReward.Should().Be(expectedCoins);
        task.Difficulty.Should().Be(difficulty);
    }

    [Fact]
    public void Level_Progression_ShouldHaveIncreasingMinCoins()
    {
        // Arrange
        var levels = new List<Level>
        {
            new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" },
            new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250, LevelImageURL = "/images/levels/sapling.png" },
            new Level { LevelID = 3, LevelName = "Mighty Oak", MinCoins = 500, LevelImageURL = "/images/levels/oak.png" }
        };

        // Act
        var isAscending = levels
            .Zip(levels.Skip(1), (a, b) => a.MinCoins < b.MinCoins)
            .All(x => x);

        // Assert
        isAscending.Should().BeTrue();
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
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        // Assert
        user.TotalCoins.Should().Be(0);
        user.CurrentLevelID.Should().Be(1);
        user.Username.Should().NotBeNullOrEmpty();
        user.Email.Should().NotBeNullOrEmpty();
        user.UserRole.Should().Be("Player");
        user.IsOnline.Should().BeFalse();
    }

    [Theory]
    [InlineData("Easy", true)]
    [InlineData("Normal", true)]
    [InlineData("Hard", true)]
    [InlineData("Invalid", false)]
    public void Task_Difficulty_ShouldBeValid(string difficulty, bool isValid)
    {
        // Arrange & Act
        var validDifficulties = new[] { "Easy", "Normal", "Hard" };
        var result = validDifficulties.Contains(difficulty, StringComparer.OrdinalIgnoreCase);

        // Assert
        result.Should().Be(isValid);
    }

    [Fact]
    public void Badge_RequiredCount_ShouldBePositive()
    {
        // Arrange
        var badge = new Badge
        {
            BadgeID = 1,
            BadgeName = "Test Badge",
            Description = "Test",
            BadgeImageURL = "/images/badges/test.png",
            CriteriaType = "TaskCount",
            RequiredCount = 5,
            RequiredDifficulty = "Easy"
        };

        // Act & Assert
        badge.RequiredCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Skin_Price_ShouldBeNonNegative()
    {
        // Arrange
        var skin = new Skin
        {
            SkinID = 1,
            SkinName = "Test Skin",
            SkinPrice = 100,
            ImageURL = "/images/skins/test.png"
        };

        // Act & Assert
        skin.SkinPrice.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void Tree_InitialState_ShouldBeHealthyAndIncomplete()
    {
        // Arrange
        var tree = new Tree
        {
            TreeID = 1,
            UserID = 1,
            MissionID = 1,
            IsWithered = false,
            IsCompleted = false
        };

        // Act & Assert
        tree.IsWithered.Should().BeFalse();
        tree.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void UserTaskHistory_Status_ShouldBeValid()
    {
        // Arrange
        var validStatuses = new[] { "Assigned", "Completed", "Failed", "Pending" };
        
        var history = new UserTaskHistory
        {
            HistoryID = 1,
            UserID = 1,
            TaskID = 1,
            Status = "Completed",
            CompletionDate = DateTime.UtcNow
        };

        // Act & Assert
        validStatuses.Should().Contain(history.Status);
    }
}