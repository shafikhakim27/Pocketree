using System.Net;
using System.Text;
using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Task = ADproject.Models.Entities.Task;

namespace Pocketree.Api.Tests.Services;

public class MlServiceTests
{
    private DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private static IConfiguration CreateConfiguration(string mlUrl)
    {
        var values = new Dictionary<string, string?>
        {
            ["MlService:Url"] = mlUrl
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public HttpRequestMessage? Request { get; private set; }

        public CapturingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return System.Threading.Tasks.Task.FromResult(_response);
        }
    }
    [Fact]
    public async System.Threading.Tasks.Task GetRecommendedTasks_FallsBackToDefaultWhenMlReturnsEmpty()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        };
        var handler = new CapturingHandler(response);
        var httpClient = new HttpClient(handler);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("ML_Consultant")).Returns(httpClient);

        var options = CreateDbOptions("MlService_Fallback_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Users.Add(new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime)
        });

        context.Tasks.AddRange(
            new Task { TaskID = 1, Description = "Default 1", Difficulty = "Easy", CoinReward = 10, Category = "General", SourceType = "Default" },
            new Task { TaskID = 2, Description = "Default 2", Difficulty = "Easy", CoinReward = 20, Category = "General", SourceType = "Default" },
            new Task { TaskID = 3, Description = "Default 3", Difficulty = "Easy", CoinReward = 30, Category = "General", SourceType = "Default" }
        );
        await context.SaveChangesAsync();

        var service = new MlService(httpClient, CreateConfiguration("https://ml.test"), context, factory.Object);

        var tasks = await service.GetRecommendedTasks(1);

        tasks.Should().HaveCount(3);
        tasks.All(t => t.SourceType == "Default").Should().BeTrue();
    }
}
