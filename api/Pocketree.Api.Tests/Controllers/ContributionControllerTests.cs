using System.Net;
using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Pocketree.Api.Tests.Helpers;

namespace Pocketree.Api.Tests.Controllers;

public class ContributionControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ContributionControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAllForestTrees_Should_ReturnCoordinates()
    {
        await SeedForestAsync(new[]
        {
            (x: 10.5, y: 20.25),
            (x: 30.0, y: 40.75)
        });

        var response = await _client.GetAsync("/api/Contribution/GetAllForestTrees");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<TreeCoordinateDto>>();
        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
        result.Should().ContainSingle(t => t.XCoordinate == 10.5 && t.YCoordinate == 20.25);
        result.Should().ContainSingle(t => t.XCoordinate == 30.0 && t.YCoordinate == 40.75);
    }

    private async System.Threading.Tasks.Task SeedForestAsync(IEnumerable<(double x, double y)> coords)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();

        db.CommunityForests.RemoveRange(db.CommunityForests);
        db.GlobalMissions.RemoveRange(db.GlobalMissions);
        await db.SaveChangesAsync();

        var mission = new GlobalMission
        {
            MissionID = 1,
            MissionName = "Test Mission",
            TotalRequiredTrees = 10,
            CurrentTreeCount = 0,
            PlantingFrequency = 1
        };

        db.GlobalMissions.Add(mission);

        var id = 1;
        foreach (var (x, y) in coords)
        {
            db.CommunityForests.Add(new CommunityForest
            {
                ForestTreeID = id++,
                XCoordinate = x,
                YCoordinate = y,
                MissionID = mission.MissionID,
                PlantedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
