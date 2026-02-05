using ADproject.Controllers;
using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using ADproject.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pocketree.Api.Tests.Controllers;

public class ContributionControllerTests
{
    private DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAllForestTrees_Should_ReturnCoordinates()
    {
        var options = CreateDbOptions("Contribution_Forest_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

                var mission = context.GlobalMissions.FirstOrDefault(m => m.MissionID == 1);
        if (mission == null)
        {
            mission = new GlobalMission
            {
                MissionID = 1,
                MissionName = "Test Mission",
                TotalRequiredTrees = 10,
                CurrentTreeCount = 0,
                PlantingFrequency = 1
            };
            context.GlobalMissions.Add(mission);
        }
        context.CommunityForests.AddRange(
            new CommunityForest { ForestTreeID = 1, XCoordinate = 10.5, YCoordinate = 20.25, MissionID = mission.MissionID, PlantedAt = DateTime.UtcNow },
            new CommunityForest { ForestTreeID = 2, XCoordinate = 30.0, YCoordinate = 40.75, MissionID = mission.MissionID, PlantedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var mockHub = new Mock<IHubContext<MapHub>>();
        var controller = new ContributionController(context, mockHub.Object);

        var result = await controller.GetAllForestTrees();

        result.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result.Result!;
        var list = ok.Value as IEnumerable<TreeCoordinateDto>;

        list.Should().NotBeNull();
        list!.Should().HaveCount(2);
        list.Should().ContainSingle(t => t.XCoordinate == 10.5 && t.YCoordinate == 20.25);
        list.Should().ContainSingle(t => t.XCoordinate == 30.0 && t.YCoordinate == 40.75);
    }
}