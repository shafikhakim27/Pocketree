using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pocketree.Api.Services;
using TaskEntity = ADproject.Models.Entities.Task;

namespace Pocketree.Api.Tests.Services;

public class DailyTaskGeneratorTests
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
            ResetCode = "",
            UncompletedTaskCount = 0
        };
    }

    [Fact]
    public async System.Threading.Tasks.Task ExecuteAsync_AssignsDailyTasks_AndUpdatesHistoryAndCounters()
    {
        var dbName = "DailyTaskGenerator_Assign_" + Guid.NewGuid();
        var options = CreateDbOptions(dbName);

        await using (var seedContext = new MyDbContext(options))
        {
            seedContext.Users.Add(CreateUser());
            await seedContext.SaveChangesAsync();
        }

        var generatedTasks = new List<TaskEntity>
        {
            new() { TaskID = 101, Description = "ML Easy", Difficulty = "Easy", CoinReward = 10, Keyword = "k1", Category = "nature", NegativeKeyword = "", SourceType = "ML" },
            new() { TaskID = 102, Description = "ML Normal", Difficulty = "Normal", CoinReward = 20, Keyword = "k2", Category = "food", NegativeKeyword = "", SourceType = "ML" },
            new() { TaskID = 103, Description = "ML Hard", Difficulty = "Hard", CoinReward = 30, RequiresEvidence = true, Keyword = "k3", Category = "reuse", NegativeKeyword = "", SourceType = "ML" }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var taskServiceMock = new Mock<ITaskService>();
        taskServiceMock
            .Setup(x => x.CleanupOldTasks(It.IsAny<User>()))
            .Returns(System.Threading.Tasks.Task.CompletedTask);

        taskServiceMock
            .Setup(x => x.FetchNewTasks(It.IsAny<User>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(generatedTasks);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => new MyDbContext(options));
        serviceCollection.AddScoped(_ => taskServiceMock.Object);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var generator = new TestableDailyTaskGenerator(serviceProvider);

        Func<System.Threading.Tasks.Task> act = async () => await generator.RunForTestAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        taskServiceMock.Verify(x => x.CleanupOldTasks(It.IsAny<User>()), Times.Once);
        taskServiceMock.Verify(x => x.FetchNewTasks(It.IsAny<User>()), Times.Once);

        await using var assertContext = new MyDbContext(options);
        var history = await assertContext.UserTaskHistory.Where(h => h.UserID == 1).ToListAsync();
        history.Should().HaveCount(3);

        var user = await assertContext.Users.FindAsync(1);
        user.Should().NotBeNull();
        user!.UncompletedTaskCount.Should().Be(3);

        var mlTasksPersisted = await assertContext.Tasks.CountAsync(t => t.SourceType == "ML");
        mlTasksPersisted.Should().Be(3);
    }

    private sealed class TestableDailyTaskGenerator : DailyTaskGenerator
    {
        public TestableDailyTaskGenerator(IServiceProvider services) : base(services)
        {
        }

        public System.Threading.Tasks.Task RunForTestAsync(CancellationToken token)
        {
            return base.ExecuteAsync(token);
        }
    }
}
