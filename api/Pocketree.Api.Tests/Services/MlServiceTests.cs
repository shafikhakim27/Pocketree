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
    private static byte[] CreateFakeImageBytes()
    {
        // Minimal valid JPEG header (just enough for stream handling in tests)
        return new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
    }

    [Fact]
    public async System.Threading.Tasks.Task ClassifyImageAsync_BaseUrl_UsesPredictEndpoint()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"verified\":true}", Encoding.UTF8, "application/json")
        };
        var handler = new CapturingHandler(response);
        var httpClient = new HttpClient(handler);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("ML_Consultant"))
            .Returns(new HttpClient(new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            })));

        var options = CreateDbOptions("MlService_Predict_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var config = CreateConfiguration("https://ml.test");
        var service = new MlService(httpClient, config, context, factory.Object);

        using var ms = new MemoryStream(CreateFakeImageBytes());
        config["MlService:Url"].Should().Be("https://ml.test");
        var result = await service.ClassifyImageAsync(ms, "tree");

        result.Should().BeTrue();
        handler.Request.Should().NotBeNull();
        handler.Request!.RequestUri!.AbsoluteUri.Should().Be("https://ml.test/predict");
        handler.Request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async System.Threading.Tasks.Task ClassifyImageAsync_ClassifyUrl_UsesMultipartForm()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"verified\":false}", Encoding.UTF8, "application/json")
        };
        var handler = new CapturingHandler(response);
        var httpClient = new HttpClient(handler);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("ML_Consultant"))
            .Returns(new HttpClient(new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            })));

        var options = CreateDbOptions("MlService_Classify_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var config = CreateConfiguration("https://ml.test/classify");
        var service = new MlService(httpClient, config, context, factory.Object);

        using var ms = new MemoryStream(CreateFakeImageBytes());
        config["MlService:Url"].Should().Be("https://ml.test/classify");
        var result = await service.ClassifyImageAsync(ms, "recycle");

        result.Should().BeFalse();
        handler.Request.Should().NotBeNull();
        handler.Request!.RequestUri!.AbsoluteUri.Should().Be("https://ml.test/classify");
        handler.Request.Content!.Headers.ContentType!.MediaType.Should().Be("multipart/form-data");
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
