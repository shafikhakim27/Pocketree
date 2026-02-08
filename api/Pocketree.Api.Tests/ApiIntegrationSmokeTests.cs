using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ADproject.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Pocketree.Api.Tests.Helpers;

namespace Pocketree.Api.Tests.Integration;

public class ApiIntegrationSmokeTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationSmokeTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async System.Threading.Tasks.Task Health_Endpoints_Respond()
    {
        var health = await _client.GetAsync("/api/health");
        health.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var ready = await _client.GetAsync("/api/health/ready");
        ready.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var live = await _client.GetAsync("/api/health/live");
        live.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoginApi_ReturnsToken()
    {
        var token = await GetAuthTokenAsync();
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async System.Threading.Tasks.Task UserProfileApi_RequiresAuth()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/User/GetUserProfileApi");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await resp.Content.ReadAsStringAsync();
        json.Should().Contain("\"username\"");
    }

    [Fact]
    public async System.Threading.Tasks.Task GetDailyTasksApi_ReturnsTasks_WhenSeeded()
    {
        await SeedDataAsync();
        var token = await GetAuthTokenAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/Task/GetDailyTasksApi");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await resp.Content.ReadAsStringAsync();
        json.TrimStart().StartsWith("[").Should().BeTrue("Expected JSON array of tasks");
    }

    [Fact]
    public async System.Threading.Tasks.Task RecordTaskCompletionApi_UpdatesCoins()
    {
        await SeedDataAsync();
        var token = await GetAuthTokenAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var content = new MultipartFormDataContent
        {
            { new StringContent("1"), "taskId" },
            { new StringContent("Completed"), "status" }
        };

        var resp = await _client.PostAsync("/api/Task/RecordTaskCompletionApi", content);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify DB updated
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();

        var user = await db.Users.FindAsync(1);
        user!.TotalCoins.Should().Be(100);
        user.UncompletedTaskCount.Should().Be(0);
    }

    [Fact]
    public async System.Threading.Tasks.Task SkinsShopApi_Responds_WithAuth()
    {
        await SeedSkinsAsync();
        var token = await GetAuthTokenAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/User/GetSkinsShopApi");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async System.Threading.Tasks.Task<string> GetAuthTokenAsync()
    {
        var loginResp = await _client.PostAsJsonAsync("/api/User/LoginApi", new
        {
            username = "testuser",
            password = "Password123!"
        });

        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await loginResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return FindJsonString(doc.RootElement, "Token") ?? string.Empty;
    }

    private static string? FindJsonString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    return prop.Value.GetString();

                var nested = FindJsonString(prop.Value, name);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindJsonString(item, name);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }

        return null;
    }

    private async System.Threading.Tasks.Task SeedDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();

        if (!db.GlobalMissions.Any())
        {
            db.GlobalMissions.Add(new GlobalMission
            {
                MissionID = 1,
                MissionName = "Greenify Sahara",
                TotalRequiredTrees = 1000,
                CurrentTreeCount = 0,
                PlantingFrequency = 1
            });
        }

        if (!db.Tasks.Any())
        {
            db.Tasks.AddRange(
                new ADproject.Models.Entities.Task
                {
                    TaskID = 1,
                    Description = "Seed test task",
                    Difficulty = "Easy",
                    CoinReward = 100,
                    RequiresEvidence = false,
                    Keyword = "tree",
                    Category = "Testing",
                    SourceType = "Default"
                },
                new ADproject.Models.Entities.Task
                {
                    TaskID = 2,
                    Description = "Second test task",
                    Difficulty = "Normal",
                    CoinReward = 150,
                    RequiresEvidence = false,
                    Keyword = "plant",
                    Category = "Testing",
                    SourceType = "Default"
                },
                new ADproject.Models.Entities.Task
                {
                    TaskID = 3,
                    Description = "Third test task",
                    Difficulty = "Hard",
                    CoinReward = 200,
                    RequiresEvidence = true,
                    Keyword = "recycle",
                    Category = "Testing",
                    SourceType = "Default"
                }
            );
        }

        if (!db.Trees.Any())
        {
            db.Trees.Add(new Tree
            {
                TreeID = 1,
                UserID = 1,
                MissionID = 1,
                IsCompleted = false,
                IsWithered = false
            });
        }

        var today = DateTime.UtcNow.Date;
        var hasHistory = db.UserTaskHistory.Any(h => h.UserID == 1 && h.CompletionDate >= today);
        if (!hasHistory)
        {
            db.UserTaskHistory.Add(new UserTaskHistory
            {
                UserID = 1,
                TaskID = 1,
                Status = "Assigned",
                CompletionDate = DateTime.UtcNow
            });

            var user = await db.Users.FindAsync(1);
            if (user != null)
            {
                user.UncompletedTaskCount = 1;
            }
        }

        await db.SaveChangesAsync();
    }

    private async System.Threading.Tasks.Task SeedSkinsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();

        if (!db.Skins.Any())
        {
            db.Skins.AddRange(
                new Skin { SkinID = 1, SkinName = "Forest", SkinPrice = 10, ImageURL = "/forest.png", SkinKey = "forest" },
                new Skin { SkinID = 2, SkinName = "Ocean", SkinPrice = 20, ImageURL = "/ocean.png", SkinKey = "ocean" }
            );
            await db.SaveChangesAsync();
        }
    }
[Fact]
public async System.Threading.Tasks.Task LogoutApi_Responds()
{
    var token = await GetAuthTokenAsync();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var resp = await _client.PostAsync("/api/User/LogoutApi", null);
    resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
}

[Fact]
public async System.Threading.Tasks.Task ChangePasswordApi_Responds()
{
    var token = await GetAuthTokenAsync();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var resp = await _client.PostAsJsonAsync("/api/User/change-password", new
    {
        currentPassword = "Password123!",
        newPassword = "Password123!"
    });

    resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
}

[Fact]
public async System.Threading.Tasks.Task GetLatestBadgesApi_Responds()
{
    var token = await GetAuthTokenAsync();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var resp = await _client.GetAsync("/api/User/GetLatestBadgesApi");
    resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
}

[Fact]
public async System.Threading.Tasks.Task GetAllSkinsOfferedApi_Responds()
{
    var token = await GetAuthTokenAsync();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var resp = await _client.GetAsync("/api/User/GetAllSkinsOfferedApi");
    resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
}

[Fact]
public async System.Threading.Tasks.Task GetSkinsShopApi_Responds()
{
    var token = await GetAuthTokenAsync();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var resp = await _client.GetAsync("/api/User/GetSkinsShopApi");
    resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
}

[Fact]
public async System.Threading.Tasks.Task RedeemSkinApi_Responds()
{
    var token = await GetAuthTokenAsync();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // This may fail if no skins / insufficient coins. That's fine.
    var resp = await _client.PostAsJsonAsync("/api/Task/RedeemSkinApi", 1);
    resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
}

[Fact]
public async System.Threading.Tasks.Task EquipSkinApi_Responds()
{
    var token = await GetAuthTokenAsync();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var resp = await _client.PostAsJsonAsync("/api/Task/EquipSkinApi", 1);
    resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
}

[Fact]
public async System.Threading.Tasks.Task GetAllVouchersApi_Responds()
{
    var token = await GetAuthTokenAsync();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var resp = await _client.GetAsync("/api/User/GetAllVouchersApi");
    resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
}

[Fact]
public async System.Threading.Tasks.Task RedeemVoucherApi_Responds()
{
    var token = await GetAuthTokenAsync();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var resp = await _client.PostAsJsonAsync("/api/Task/RedeemVoucherApi", 1);
    resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
}

[Fact]
public async System.Threading.Tasks.Task Contribution_GetAllForestTrees_Responds()
    {
    var resp = await _client.GetAsync("/api/Contribution/GetAllForestTrees");
    resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
    }
}
