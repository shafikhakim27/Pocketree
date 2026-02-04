using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Pocketree.Api.Tests.Helpers;

namespace Pocketree.Api.Tests.Controllers;

public class HealthControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public HealthControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async System.Threading.Tasks.Task Live_Should_ReturnHealthyStatus()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/Health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        doc.RootElement.GetProperty("service").GetString().Should().Be("Pocketree API");
    }

    [Fact]
    public async System.Threading.Tasks.Task Ready_Should_ReturnHealthyStatus()
    {
        using var client = CreateClientWithMlUrl("http://127.0.0.1:9");

        var response = await client.GetAsync("/api/Health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        var checks = doc.RootElement.GetProperty("checks");
        checks.GetProperty("database").GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async System.Threading.Tasks.Task Health_Should_ReturnHealthyStatus()
    {
        using var client = CreateClientWithMlUrl("http://127.0.0.1:9");

        var response = await client.GetAsync("/api/Health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        var checks = doc.RootElement.GetProperty("checks");
        checks.GetProperty("configuration").GetProperty("status").GetString().Should().Be("Healthy");
    }

    private HttpClient CreateClientWithMlUrl(string url)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["ML_SERVICE_URL"] = url,
                    ["HealthChecks:MlTimeoutSeconds"] = "1"
                };

                config.AddInMemoryCollection(overrides);
            });
        });

        return factory.CreateClient();
    }
}
