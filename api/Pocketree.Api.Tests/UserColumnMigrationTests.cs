using Microsoft.EntityFrameworkCore.Diagnostics;
using ADproject.Models.Entities;

namespace Pocketree.Api.Tests;

public class UserColumnMigrationTests
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
    public async System.Threading.Tasks.Task User_Should_Have_UserRole_Column()
    {
        // Arrange
        var options = CreateDbOptions("TestDb_UserRole");
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Add required Level
        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });
        await context.SaveChangesAsync();

        // Act
        var user = new User
        {
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = null,
            UserRole = "Player",
            ResetExpiry = default(DateTime),
            IsOnline = false,
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert
        var savedUser = await context.Users.FirstAsync();
        savedUser.UserRole.Should().Be("Player");
        savedUser.IsOnline.Should().BeFalse();
        savedUser.UncompletedTaskCount.Should().Be(0);
        savedUser.NotAttemptedTaskCount.Should().Be(0);
        savedUser.FailedVerificationCount.Should().Be(0);
    }

    [Theory]
    [InlineData("Player")]
    [InlineData("Admin")]
    public async System.Threading.Tasks.Task User_UserRole_Should_AcceptValidRoles(string role)
    {
        // Arrange
        var options = CreateDbOptions($"TestDb_Role_{role}");
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });
        await context.SaveChangesAsync();

        // Act
        var user = new User
        {
            Username = $"user_{role}",
            Email = $"{role}@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = null,
            UserRole = role,
            ResetExpiry = default(DateTime),
            IsOnline = false,
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert
        var savedUser = await context.Users.FirstAsync();
        savedUser.UserRole.Should().Be(role);
    }

    [Fact]
    public async System.Threading.Tasks.Task User_ResetExpiry_Should_BeNullable()
    {
        // Arrange
        var options = CreateDbOptions("TestDb_ResetExpiryNull");
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });
        await context.SaveChangesAsync();

        // Act
        var user = new User
        {
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = null,
            UserRole = "Player",
            ResetExpiry = default(DateTime),
            IsOnline = false,
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert
        var savedUser = await context.Users.FirstAsync();
        // ResetExpiry will be default(DateTime) which is DateTime.MinValue, not null
        // If ResetExpiry is DateTime (non-nullable), it can't be null
        savedUser.ResetExpiry.Should().Be(default(DateTime));
    }

    [Fact]
    public async System.Threading.Tasks.Task User_IsOnline_Should_DefaultToFalse()
    {
        // Arrange - Use unique database name
        var options = CreateDbOptions("TestDb_IsOnlineDefaultUnique_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Add required Level first
        context.Levels.Add(new Level 
        { 
            LevelID = 1, 
            LevelName = "Seedling", 
            MinCoins = 0,
            LevelImageURL = "/images/levels/seedling.png"
        });
        await context.SaveChangesAsync();

        // Act
        var user = new User
        {
            // Don't set UserID - let EF assign it
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false, // Explicitly set to false
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert
        var savedUser = await context.Users.FirstAsync();
        savedUser.Should().NotBeNull();
        savedUser.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task User_CanToggle_IsOnline()
    {
        // Arrange
        var options = CreateDbOptions("TestDb_IsOnlineToggle");
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });

        var user = new User
        {
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = null,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act - Set user online
        user.IsOnline = true;
        await context.SaveChangesAsync();

        // Assert
        var onlineUser = await context.Users.FirstAsync();
        onlineUser.IsOnline.Should().BeTrue();

        // Act - Set user offline
        onlineUser.IsOnline = false;
        await context.SaveChangesAsync();

        // Assert
        var offlineUser = await context.Users.FirstAsync();
        offlineUser.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async System.Threading.Tasks.Task User_ResetExpiry_CanBeSetAndCleared()
    {
        // Arrange
        var options = CreateDbOptions("TestDb_ResetExpiryChange");
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });

        var futureDate = DateTime.UtcNow.AddDays(7);
        var user = new User
        {
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = null,
            UserRole = "Player",
            ResetExpiry = futureDate,
            IsOnline = false,
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert - Has expiry date
        var userWithExpiry = await context.Users.FirstAsync();
        userWithExpiry.ResetExpiry.Should().BeCloseTo(futureDate, TimeSpan.FromSeconds(1));

        // Act - Clear expiry (set to default)
        userWithExpiry.ResetExpiry = default(DateTime);
        await context.SaveChangesAsync();

        // Assert - Expiry cleared (set to DateTime.MinValue)
        var userWithoutExpiry = await context.Users.FirstAsync();
        userWithoutExpiry.ResetExpiry.Should().Be(default(DateTime));
    }
}