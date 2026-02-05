using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ADproject.Models.Entities;

namespace Pocketree.Api.Tests;

public class DatabaseInitializationTests
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
    public async System.Threading.Tasks.Task InitDBShouldCreateSeedUsersWithNewColumns()
    {
        // Arrange
        var options = CreateDbOptions("TestDb_SeedUsers");
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

        // Act - Simulate seed data from Program.cs
        if (!context.Users.Any())
        {
            context.Users.AddRange(
                new User
                {
                    UserID = 1,
                    Username = "ecotester",
                    PasswordHash = "AQAAAAIAAYagAAAAEMO7BqP3P6mwKCn+y4U448SilNgQsmcaKZlFou2pu3x/3EiFixI8pLMryKFzJWQbOA==",
                    Email = "ecotester@gmail.com",
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
                },
                new User
                {
                    UserID = 2,
                    Username = "ecoadmin",
                    PasswordHash = "AQAAAAIAAYagAAAAEMO7BqP3P6mwKCn+y4U448SilNgQsmcaKZlFou2pu3x/3EiFixI8pLMryKFzJWQbOA==",
                    Email = "ecoadmin@gmail.com",
                    ProfileImageURL = "/images/default-user.jpg",
                    TotalCoins = 0,
                    CurrentLevelID = 1,
                    LastLoginDate = DateTime.UtcNow,
                    LastActivityDate = null,
                    UserRole = "Admin",
                    ResetExpiry = default(DateTime),
                    IsOnline = false,
                    UncompletedTaskCount = 0,
                    NotAttemptedTaskCount = 0,
                    FailedVerificationCount = 0
                }
            );
            await context.SaveChangesAsync();
        }

        // Assert
        var users = await context.Users.ToListAsync();
        users.Should().HaveCount(2);

        var player = users.First(u => u.Username == "ecotester");
        player.UserRole.Should().Be("Player");
        player.IsOnline.Should().BeFalse();
        player.UncompletedTaskCount.Should().Be(0);
        player.NotAttemptedTaskCount.Should().Be(0);
        player.FailedVerificationCount.Should().Be(0);

        var admin = users.First(u => u.Username == "ecoadmin");
        admin.UserRole.Should().Be("Admin");
        admin.IsOnline.Should().BeFalse();
        admin.UncompletedTaskCount.Should().Be(0);
    }

    [Fact]
    public async System.Threading.Tasks.Task Database_Should_Seed_AllRequiredEntities()
    {
        // Arrange
        var options = CreateDbOptions("TestDb_AllSeeds");
        using var context = new MyDbContext(options);

        // Act - Seed all data like Program.cs does
        context.Levels.AddRange(
            new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" },
            new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250, LevelImageURL = "/images/levels/sapling.png" },
            new Level { LevelID = 3, LevelName = "Mighty Oak", MinCoins = 500, LevelImageURL = "/images/levels/oak.png" }
        );

        context.Tasks.AddRange(
            new ADproject.Models.Entities.Task 
            { 
                TaskID = 1, 
                Description = "Turn off lights", 
                Difficulty = "Easy", 
                CoinReward = 100, 
                Category = "Energy Saving",
                RequiresEvidence = false,
                Keyword = "switch"
            },
            new ADproject.Models.Entities.Task 
            { 
                TaskID = 2, 
                Description = "Reusable bottle", 
                Difficulty = "Easy", 
                CoinReward = 100, 
                Category = "Recycling",
                RequiresEvidence = false,
                Keyword = "bottle"
            }
        );

        context.Badges.Add(new Badge 
        { 
            BadgeID = 1, 
            BadgeName = "Tree Starter", 
            Description = "Reach Level 2",
            BadgeImageURL = "",
            CriteriaType = "LevelUp", 
            RequiredDifficulty = "Any",
            RequiredCount = 2 
        });

        context.Skins.Add(new Skin 
        { 
            SkinID = 1, 
            SkinName = "Item1", 
            SkinPrice = 10,
            ImageURL = "",
            SkinKey = "item1"
        });

        context.Vouchers.Add(new Voucher 
        { 
            VoucherID = 1, 
            VoucherName = "Voucher 1",
            Description = "Earned at Level 2",
            MinRedemptionLevel = 2 
        });

        await context.SaveChangesAsync();

        // Assert
        context.Levels.Should().HaveCount(3);
        context.Tasks.Should().HaveCount(2);
        context.Badges.Should().HaveCount(1);
        context.Skins.Should().HaveCount(1);
        context.Vouchers.Should().HaveCount(1);
    }

    [Fact]
    public async System.Threading.Tasks.Task SeedData_Should_CreateDefaultPlayerRole()
    {
        // Arrange
        var options = CreateDbOptions("TestDb_DefaultRole");
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Levels.Add(new Level 
        { 
            LevelID = 1, 
            LevelName = "Seedling", 
            MinCoins = 0,
            LevelImageURL = "/images/levels/seedling.png"
        });
        await context.SaveChangesAsync();

        // Act - Create user without explicitly setting role
        var user = new User
        {
            UserID = 1,
            Username = "newuser",
            Email = "new@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = null, // ? ADDED for clarity
            UserRole = "Player", // Default role
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert
        var savedUser = await context.Users.FindAsync(1);
        savedUser.Should().NotBeNull();
        savedUser!.UserRole.Should().Be("Player");
    }

    [Fact]
    public async System.Threading.Tasks.Task SeedData_Should_SetIsOnlineToFalse()
    {
        // Arrange
        var options = CreateDbOptions("TestDb_IsOnlineDefault");
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

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
            UserID = 1,
            Username = "offlineuser",
            Email = "offline@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = null, // ? ADDED for clarity
            UserRole = "Player",
            IsOnline = false, // Should be false by default
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert
        var savedUser = await context.Users.FindAsync(1);
        savedUser!.IsOnline.Should().BeFalse();
    }
}
