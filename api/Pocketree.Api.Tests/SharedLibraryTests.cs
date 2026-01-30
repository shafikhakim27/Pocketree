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
        Assert.Equal(expected, result);
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
        Assert.Equal(expected, result);
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
        Assert.Equal(expected, result);
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
        Assert.Equal(expected, result);
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
        Assert.Equal(expected, result);
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
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DateTimeExtensions_IsToday_ShouldReturnTrueForToday()
    {
        // Arrange
        var today = DateTime.UtcNow;

        // Act
        var result = today.IsToday();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DateTimeExtensions_IsToday_ShouldReturnFalseForYesterday()
    {
        // Arrange
        var yesterday = DateTime.UtcNow.AddDays(-1);

        // Act
        var result = yesterday.IsToday();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DateTimeExtensions_DaysSince_ShouldCalculateCorrectly()
    {
        // Arrange
        var threeDaysAgo = DateTime.UtcNow.AddDays(-3).Date;

        // Act
        var result = threeDaysAgo.DaysSince();

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void Result_Ok_ShouldCreateSuccessResult()
    {
        // Act
        var result = Result.Ok("Operation successful");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Operation successful", result.Message);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Result_Fail_ShouldCreateFailureResult()
    {
        // Act
        var result = Result.Fail("Operation failed");

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Equal("Operation failed", result.Errors[0]);
    }

    [Fact]
    public void ResultT_Ok_ShouldCreateSuccessResultWithData()
    {
        // Arrange
        var testData = new { Name = "Test", Value = 123 };

        // Act
        var result = Result<object>.Ok(testData, "Data retrieved");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Data retrieved", result.Message);
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
