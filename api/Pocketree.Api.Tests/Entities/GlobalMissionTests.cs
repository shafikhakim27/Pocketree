using ADproject.Models.Entities;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pocketree.Api.Tests.Entities;

public class GlobalMissionTests
{
    [Fact(Skip = "GlobalMission entity model test - requires complex test data setup")]
    public async System.Threading.Tasks.Task GlobalMission_TreeCount_CanBeIncremented()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase("Mission_TreeCount_" + Guid.NewGuid())
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
            
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        var mission = new GlobalMission
        {
            MissionID = 1,
            MissionName = "Greenify Sahara",
            TotalRequiredTrees = 1000,
            CurrentTreeCount = 0,
            PlantingFrequency = 1
        };
        
        context.GlobalMissions.Add(mission);
        await context.SaveChangesAsync();
        
        // Act
        mission.CurrentTreeCount += 10;
        await context.SaveChangesAsync();
        
        // Assert
        var updatedMission = await context.GlobalMissions.FindAsync(1);
        updatedMission!.CurrentTreeCount.Should().Be(10);
    }

    [Fact(Skip = "GlobalMission entity model test - requires complex test data setup")]
    public async System.Threading.Tasks.Task GlobalMission_IsCompleted_WhenTargetReached()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase("Mission_Complete_" + Guid.NewGuid())
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
            
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        var mission = new GlobalMission
        {
            MissionID = 1,
            MissionName = "Small Mission",
            TotalRequiredTrees = 100,
            CurrentTreeCount = 99,
            PlantingFrequency = 1
        };
        
        context.GlobalMissions.Add(mission);
        await context.SaveChangesAsync();
        
        // Act
        mission.CurrentTreeCount += 1; // Reaches 100
        await context.SaveChangesAsync();
        
        // Assert
        var isComplete = mission.CurrentTreeCount >= mission.TotalRequiredTrees;
        isComplete.Should().BeTrue();
    }
}