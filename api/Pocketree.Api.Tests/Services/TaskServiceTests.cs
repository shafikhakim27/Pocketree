using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pocketree.Api.Services;
using TaskEntity = ADproject.Models.Entities.Task;

namespace Pocketree.Api.Tests.Services;

public class TaskServiceTests
{
    private static DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private static User CreateUser(int userId = 1)
    {
        return new User
        {
            UserID = userId,
            Username = $"user{userId}",
            Email = $"user{userId}@test.com",
            PasswordHash = "hash",
            CurrentLevelID = 1,
            UserRole = "Player",
            ResetCode = ""
        };
    }

    [Fact]
    public async System.Threading.Tasks.Task CleanupOldTasks_RemovesOnlyAssignedFromPreviousDays_AndUpdatesCounters()
    {
        var options = CreateDbOptions("TaskService_Cleanup_" + Guid.NewGuid());
        await using var context = new MyDbContext(options);

        var user = CreateUser();
        user.UncompletedTaskCount = 5;
        user.NotAttemptedTaskCount = 2;

        context.Users.Add(user);
        context.UserTaskHistory.AddRange(
            new UserTaskHistory
            {
                UserID = user.UserID,
                TaskID = 1,
                Status = "Assigned",
                CompletionDate = DateTime.UtcNow.Date.AddDays(-1)
            },
            new UserTaskHistory
            {
                UserID = user.UserID,
                TaskID = 2,
                Status = "Assigned",
                CompletionDate = DateTime.UtcNow.Date.AddDays(-3)
            },
            new UserTaskHistory
            {
                UserID = user.UserID,
                TaskID = 3,
                Status = "Assigned",
                CompletionDate = DateTime.UtcNow.Date
            },
            new UserTaskHistory
            {
                UserID = user.UserID,
                TaskID = 4,
                Status = "Completed",
                CompletionDate = DateTime.UtcNow.Date.AddDays(-2)
            }
        );

        await context.SaveChangesAsync();

        var mockMlService = new Mock<IMlService>(MockBehavior.Strict);
        var service = new TaskService(context, mockMlService.Object);

        await service.CleanupOldTasks(user);

        var remainingHistory = await context.UserTaskHistory.ToListAsync();
        remainingHistory.Should().HaveCount(2);
        remainingHistory.Should().OnlyContain(h =>
            (h.Status == "Assigned" && h.CompletionDate >= DateTime.UtcNow.Date) ||
            h.Status == "Completed");

        user.NotAttemptedTaskCount.Should().Be(4);
        user.UncompletedTaskCount.Should().Be(3);
    }

    [Fact]
    public async System.Threading.Tasks.Task CleanupOldTasks_WhenNoPreviousAssignedTasks_DoesNotChangeCounters()
    {
        var options = CreateDbOptions("TaskService_Cleanup_NoOp_" + Guid.NewGuid());
        await using var context = new MyDbContext(options);

        var user = CreateUser();
        user.UncompletedTaskCount = 2;
        user.NotAttemptedTaskCount = 1;

        context.Users.Add(user);
        context.UserTaskHistory.AddRange(
            new UserTaskHistory
            {
                UserID = user.UserID,
                TaskID = 1,
                Status = "Completed",
                CompletionDate = DateTime.UtcNow.Date.AddDays(-1)
            },
            new UserTaskHistory
            {
                UserID = user.UserID,
                TaskID = 2,
                Status = "Assigned",
                CompletionDate = DateTime.UtcNow.Date
            }
        );
        await context.SaveChangesAsync();

        var mockMlService = new Mock<IMlService>(MockBehavior.Strict);
        var service = new TaskService(context, mockMlService.Object);

        await service.CleanupOldTasks(user);

        var history = await context.UserTaskHistory.ToListAsync();
        history.Should().HaveCount(2);
        user.UncompletedTaskCount.Should().Be(2);
        user.NotAttemptedTaskCount.Should().Be(1);
    }

    [Fact]
    public async System.Threading.Tasks.Task FetchNewTasks_WhenMlRecommendationEnabled_UsesMlServiceResults()
    {
        var options = CreateDbOptions("TaskService_Fetch_ML_" + Guid.NewGuid());
        await using var context = new MyDbContext(options);

        var user = CreateUser();
        context.Users.Add(user);
        context.UserSettings.Add(new UserSettings
        {
            UserID = user.UserID,
            UseMlRecommendation = true
        });
        await context.SaveChangesAsync();

        var expected = new List<TaskEntity>
        {
            new() { TaskID = 11, Description = "ML Easy", Difficulty = "Easy", CoinReward = 10, Keyword = "k1", Category = "nature", NegativeKeyword = "", SourceType = "ML" },
            new() { TaskID = 12, Description = "ML Normal", Difficulty = "Normal", CoinReward = 20, Keyword = "k2", Category = "food", NegativeKeyword = "", SourceType = "ML" },
            new() { TaskID = 13, Description = "ML Hard", Difficulty = "Hard", CoinReward = 30, RequiresEvidence = true, Keyword = "k3", Category = "reuse", NegativeKeyword = "", SourceType = "ML" }
        };

        var mockMlService = new Mock<IMlService>();
        mockMlService
            .Setup(x => x.GetRecommendedTasks(user.UserID))
            .ReturnsAsync(expected);

        var service = new TaskService(context, mockMlService.Object);

        var result = await service.FetchNewTasks(user);

        result.Should().BeEquivalentTo(expected);
        mockMlService.Verify(x => x.GetRecommendedTasks(user.UserID), Times.Once);
    }

    [Fact]
    public async System.Threading.Tasks.Task FetchNewTasks_WhenMlRecommendationDisabled_ReturnsDefaultOnePerDifficulty()
    {
        var options = CreateDbOptions("TaskService_Fetch_Default_" + Guid.NewGuid());
        await using var context = new MyDbContext(options);

        var user = CreateUser();
        context.Users.Add(user);
        context.UserSettings.Add(new UserSettings
        {
            UserID = user.UserID,
            UseMlRecommendation = false
        });

        context.Tasks.AddRange(
            new TaskEntity { TaskID = 21, Description = "Default Easy", Difficulty = "Easy", CoinReward = 10, Keyword = "easy", Category = "nature", NegativeKeyword = "", SourceType = "Default" },
            new TaskEntity { TaskID = 22, Description = "Default Normal", Difficulty = "Normal", CoinReward = 20, Keyword = "normal", Category = "nature", NegativeKeyword = "", SourceType = "Default" },
            new TaskEntity { TaskID = 23, Description = "Default Hard", Difficulty = "Hard", CoinReward = 30, RequiresEvidence = true, Keyword = "hard", Category = "nature", NegativeKeyword = "", SourceType = "Default" }
        );

        await context.SaveChangesAsync();

        var mockMlService = new Mock<IMlService>(MockBehavior.Strict);
        var service = new TaskService(context, mockMlService.Object);

        var result = await service.FetchNewTasks(user);

        result.Should().HaveCount(3);
        result.Should().OnlyContain(t => t != null);
        result.Select(t => t.Difficulty).Should().BeEquivalentTo(new[] { "Easy", "Normal", "Hard" });

        mockMlService.Verify(x => x.GetRecommendedTasks(It.IsAny<int>()), Times.Never);
    }
}
