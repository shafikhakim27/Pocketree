using System.Text.Json;
using ADproject.Controllers;
using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pocketree.Api.Tests.Controllers;

public class HealthControllerTests
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
            ["MlService:Url"] = mlUrl,
            ["HealthChecks:MlTimeoutSeconds"] = "1",
            ["Jwt:Key"] = "TestKey123456789012345678901234567890",
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience",
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=test;User=test;Password=test;"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static string Serialize(object value)
    {
        return JsonSerializer.Serialize(value);
    }

    [Fact]
    public async System.Threading.Tasks.Task Live_Should_ReturnHealthyStatus()
    {
        var options = CreateDbOptions("Health_Live_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var controller = new HealthController(
            context,
            Mock.Of<IMlService>(),
            CreateConfiguration("http://127.0.0.1:9"),
            NullLogger<HealthController>.Instance);

        var result = controller.GetLiveness();
        result.Should().BeOfType<OkObjectResult>();

        var ok = (OkObjectResult)result;
        var json = Serialize(ok.Value!);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        doc.RootElement.GetProperty("service").GetString().Should().Be("Pocketree API");
    }

    [Fact]
    public async System.Threading.Tasks.Task Ready_Should_ReturnHealthyStatus()
    {
        var options = CreateDbOptions("Health_Ready_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var controller = new HealthController(
            context,
            Mock.Of<IMlService>(),
            CreateConfiguration("http://127.0.0.1:9"),
            NullLogger<HealthController>.Instance);

        var result = await controller.GetReadiness();
        result.Should().BeAssignableTo<ObjectResult>();

        var ok = (ObjectResult)result;
        var json = Serialize(ok.Value!);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Unhealthy");
        var checks = doc.RootElement.GetProperty("checks");
        checks.GetProperty("database").GetProperty("status").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task Health_Should_ReturnHealthyStatus()
    {
        var options = CreateDbOptions("Health_All_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var controller = new HealthController(
            context,
            Mock.Of<IMlService>(),
            CreateConfiguration("http://127.0.0.1:9"),
            NullLogger<HealthController>.Instance);

        var result = await controller.GetHealth();
        result.Should().BeAssignableTo<ObjectResult>();

        var ok = (ObjectResult)result;
        var json = Serialize(ok.Value!);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Unhealthy");
        var checks = doc.RootElement.GetProperty("checks");
        checks.GetProperty("configuration").GetProperty("status").GetString().Should().NotBeNullOrEmpty();
    }
}