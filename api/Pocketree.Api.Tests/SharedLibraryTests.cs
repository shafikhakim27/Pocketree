using Pocketree.Shared.Constants;
using Pocketree.Shared.Helpers;
using Pocketree.Shared.Extensions;
using Pocketree.Shared.Models;

namespace Pocketree.Api.Tests;

public class SharedLibraryTests
{
    [Theory]
    [InlineData("Easy", true)]
    [InlineData("Normal", true)]
    [InlineData("Hard", true)]
    [InlineData("easy", true)] // Case insensitive
    [InlineData("Invalid", false)]
    [InlineData("", false)]
    public void Difficulty_IsValid_ShouldValidateCorrectly(string difficulty, bool expected)
    {
        // Act
        var result = AppConstants.Difficulty.IsValid(difficulty);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Easy", 100)]
    [InlineData("Normal", 200)]
    [InlineData("Hard", 300)]
    public void CoinRewards_GetRewardForDifficulty_ShouldReturnCorrectValue(string difficulty, int expected)
    {
        // Act
        var result = AppConstants.CoinRewards.GetRewardForDifficulty(difficulty);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user.name@domain.co.uk", true)]
    [InlineData("invalid.email", false)]
    [InlineData("@example.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidationHelper_IsValidEmail_ShouldValidateCorrectly(string? email, bool expected)
    {
        // Act
        var result = ValidationHelper.IsValidEmail(email);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("validuser123", true)]
    [InlineData("user_name", true)]
    [InlineData("ab", false)] // Too short
    [InlineData("thisusernameistoolongforvalidation", false)] // Too long
    [InlineData("user-name", false)] // Invalid character
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidationHelper_IsValidUsername_ShouldValidateCorrectly(string? username, bool expected)
    {
        // Act
        var result = ValidationHelper.IsValidUsername(username);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Password123", true)]
    [InlineData("SecurePass1", true)]
    [InlineData("short1", false)] // Too short
    [InlineData("noDigitsHere", false)] // No digits
    [InlineData("12345678", false)] // No letters
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidationHelper_IsValidPassword_ShouldValidateCorrectly(string? password, bool expected)
    {
        // Act
        var result = ValidationHelper.IsValidPassword(password);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(50.0, 50.0, true)]
    [InlineData(0.0, 0.0, true)]
    [InlineData(100.0, 100.0, true)]
    [InlineData(-1.0, 50.0, false)]
    [InlineData(50.0, 101.0, false)]
    public void ValidationHelper_AreValidCoordinates_ShouldValidateCorrectly(double x, double y, bool expected)
    {
        // Act
        var result = ValidationHelper.AreValidCoordinates(x, y);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void DateTimeExtensions_IsToday_ShouldReturnTrueForToday()
    {
        // Arrange
        var today = DateTime.UtcNow;

        // Act
        var result = today.IsToday();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void DateTimeExtensions_IsToday_ShouldReturnFalseForYesterday()
    {
        // Arrange
        var yesterday = DateTime.UtcNow.AddDays(-1);

        // Act
        var result = yesterday.IsToday();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void DateTimeExtensions_DaysSince_ShouldCalculateCorrectly()
    {
        // Arrange
        var threeDaysAgo = DateTime.UtcNow.AddDays(-3).Date;

        // Act
        var result = threeDaysAgo.DaysSince();

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public void Result_Ok_ShouldCreateSuccessResult()
    {
        // Act
        var result = Result.Ok("Operation successful");

        // Assert
        result.Should().BeEquivalentTo(new { Success = true, Message = "Operation successful", Errors = Array.Empty<string>() });
    }

    [Fact]
    public void Result_Fail_ShouldCreateFailureResult()
    {
        // Act
        var result = Result.Fail("Operation failed");

        // Assert
        result.Should().BeEquivalentTo(new { Success = false, Errors = new[] { "Operation failed" } });
    }

    [Fact]
    public void ResultT_Ok_ShouldCreateSuccessResultWithData()
    {
        // Arrange
        var testData = new { Name = "Test", Value = 123 };

        // Act
        var result = Result<object>.Ok(testData, "Data retrieved");

        // Assert
        result.Should().BeEquivalentTo(new { Success = true, Data = testData, Message = "Data retrieved" });
    }

    [Fact]
    public void AppConstants_Categories_ShouldContainAllExpectedValues()
    {
        // Assert
        Assert.Contains(AppConstants.Categories.EnergySaving, AppConstants.Categories.All);
        Assert.Contains(AppConstants.Categories.Recycling, AppConstants.Categories.All);
        Assert.Contains(AppConstants.Categories.WaterSaving, AppConstants.Categories.All);
        Assert.Contains(AppConstants.Categories.Nature, AppConstants.Categories.All);
        Assert.Equal(4, AppConstants.Categories.All.Length);
    }

    [Fact]
    public void AppConstants_Difficulty_ShouldContainAllExpectedValues()
    {
        // Assert
        Assert.Contains(AppConstants.Difficulty.Easy, AppConstants.Difficulty.All);
        Assert.Contains(AppConstants.Difficulty.Normal, AppConstants.Difficulty.All);
        Assert.Contains(AppConstants.Difficulty.Hard, AppConstants.Difficulty.All);
        Assert.Equal(3, AppConstants.Difficulty.All.Length);
    }
}
