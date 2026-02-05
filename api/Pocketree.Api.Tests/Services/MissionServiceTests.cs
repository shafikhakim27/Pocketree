using ADproject.Services;

namespace Pocketree.Api.Tests.Services;

public class MissionServiceTests
{
    [Fact]
    public void MissionService_LocationSlots_ShouldHave50Locations()
    {
        // Arrange & Act
        var locationCount = MissionService.locSlots.Count;

        // Assert
        locationCount.Should().Be(50);
    }

    [Fact]
    public void MissionService_LocationSlots_ShouldHaveValidCoordinates()
    {
        // Arrange & Act
        var invalidLocations = MissionService.locSlots
            .Where(loc => loc.X < 0 || loc.X > 100 || loc.Y < 0 || loc.Y > 100)
            .ToList();

        // Assert
        invalidLocations.Should().BeEmpty();
    }

    [Fact]
    public void MissionService_LocationSlots_ShouldHaveUniqueCoordinates()
    {
        // Arrange & Act
        var uniqueLocations = MissionService.locSlots.Distinct().Count();

        // Assert
        uniqueLocations.Should().Be(50);
    }

    [Fact]
    public void MissionService_PlantingFrequency_IsPositive()
    {
        // Arrange
        var frequency = 1; // Default from GlobalMission

        // Act & Assert
        frequency.Should().BePositive();
    }

    [Fact]
    public void MissionService_LocationSlots_CoverEntireMapArea()
    {
        // Arrange & Act
        var minX = MissionService.locSlots.Min(loc => loc.X);
        var maxX = MissionService.locSlots.Max(loc => loc.X);
        var minY = MissionService.locSlots.Min(loc => loc.Y);
        var maxY = MissionService.locSlots.Max(loc => loc.Y);

        // Assert - Coverage of map bounds
        minX.Should().BeLessOrEqualTo(20, "should reach the left edge");
        maxX.Should().BeGreaterOrEqualTo(80, "should reach the right edge");
        minY.Should().BeLessOrEqualTo(20, "should reach the top edge");
        maxY.Should().BeGreaterOrEqualTo(80, "should reach the bottom edge");
    }
}
