using ADproject.Models.Entities;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pocketree.Api.Tests.Entities;

public class VoucherTests
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
    public async System.Threading.Tasks.Task Voucher_CanBeCreatedAndRetrieved()
    {
        // Arrange
        var options = CreateDbOptions("Voucher_CRUD_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        var voucher = new Voucher
        {
            VoucherID = 1,
            VoucherName = "Level 2 Reward",
            Description = "Earned at Level 2",
            MinRedemptionLevel = 2
        };
        
        context.Vouchers.Add(voucher);
        await context.SaveChangesAsync();
        
        // Act
        var savedVoucher = await context.Vouchers.FindAsync(1);
        
        // Assert
        savedVoucher.Should().NotBeNull();
        savedVoucher!.VoucherName.Should().Be("Level 2 Reward");
        savedVoucher.MinRedemptionLevel.Should().Be(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task Voucher_MinRedemptionLevel_ShouldPreventEarlyRedemption()
    {
        // Arrange
        var options = CreateDbOptions("Voucher_Level_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        context.Levels.AddRange(
            new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" },
            new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250, LevelImageURL = "/images/levels/sapling.png" }
        );
        
        var voucher = new Voucher
        {
            VoucherID = 1,
            VoucherName = "Premium Voucher",
            Description = "Level 2+ only",
            MinRedemptionLevel = 2
        };
        
        var userLevel1 = new User
        {
            UserID = 1,
            Username = "level1user",
            Email = "level1@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1, // Below min level
            TotalCoins = 100,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
        
        context.Vouchers.Add(voucher);
        context.Users.Add(userLevel1);
        await context.SaveChangesAsync();
        
        // Act
        var canRedeem = userLevel1.CurrentLevelID >= voucher.MinRedemptionLevel;
        
        // Assert
        canRedeem.Should().BeFalse();
    }
}