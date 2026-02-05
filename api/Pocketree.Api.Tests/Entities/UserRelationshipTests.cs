using ADproject.Models.Entities;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pocketree.Api.Tests.Entities;

public class UserRelationshipTests
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
    public async System.Threading.Tasks.Task UserBadge_CanBeAwarded()
    {
        // Arrange
        var options = CreateDbOptions("UserBadge_Award_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1,
            TotalCoins = 0,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
        
        var badge = new Badge
        {
            BadgeID = 1,
            BadgeName = "First Steps",
            Description = "Complete your first task",
            BadgeImageURL = "/images/badges/first.png",
            CriteriaType = "TaskCount",
            RequiredCount = 1,
            RequiredDifficulty = "Any"
        };
        
        context.Users.Add(user);
        context.Badges.Add(badge);
        await context.SaveChangesAsync();
        
        // Act
        var userBadge = new UserBadge
        {
            UserBadgeID = 1,
            UserID = 1,
            BadgeID = 1,
            DateEarned = DateTime.UtcNow
        };
        
        context.UserBadges.Add(userBadge);
        await context.SaveChangesAsync();
        
        // Assert
        var awardedBadge = await context.UserBadges
            .FirstOrDefaultAsync(ub => ub.UserID == 1 && ub.BadgeID == 1);
            
        awardedBadge.Should().NotBeNull();
        awardedBadge!.DateEarned.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async System.Threading.Tasks.Task UserSkin_CanBeEquipped()
    {
        // Arrange
        var options = CreateDbOptions("UserSkin_Equip_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1,
            TotalCoins = 500,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
        
        var skin = new Skin
        {
            SkinID = 1,
            SkinName = "Golden Tree",
            SkinPrice = 300,
            ImageURL = "/images/skins/golden.png",
            SkinKey = "golden"
        };
        
        context.Users.Add(user);
        context.Skins.Add(skin);
        await context.SaveChangesAsync();
        
        // Act - Purchase skin
        var userSkin = new UserSkin
        {
            UserSkinID = 1,
            UserID = 1,
            SkinID = 1,
            RedemptionDate = DateTime.UtcNow,
            IsEquipped = false
        };
        context.UserSkins.Add(userSkin);
        await context.SaveChangesAsync();
        
        // Equip the skin
        userSkin.IsEquipped = true;
        await context.SaveChangesAsync();
        
        // Assert
        var equippedSkin = await context.UserSkins
            .FirstOrDefaultAsync(us => us.UserID == 1 && us.SkinID == 1);
            
        equippedSkin.Should().NotBeNull();
        equippedSkin!.IsEquipped.Should().BeTrue();
    }

    [Fact]
    public async System.Threading.Tasks.Task UserSkin_OnlyOneCanBeEquipped()
    {
        // Arrange
        var options = CreateDbOptions("UserSkin_OneEquipped_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        context.Levels.Add(new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" });
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1,
            TotalCoins = 1000,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
        
        var skin1 = new Skin { SkinID = 1, SkinName = "Skin 1", SkinPrice = 100, ImageURL = "/images/skins/1.png", SkinKey = "skin1" };
        var skin2 = new Skin { SkinID = 2, SkinName = "Skin 2", SkinPrice = 100, ImageURL = "/images/skins/2.png", SkinKey = "skin2" };
        
        context.Users.Add(user);
        context.Skins.AddRange(skin1, skin2);
        await context.SaveChangesAsync();
        
        var userSkin1 = new UserSkin { UserSkinID = 1, UserID = 1, SkinID = 1, RedemptionDate = DateTime.UtcNow, IsEquipped = true };
        var userSkin2 = new UserSkin { UserSkinID = 2, UserID = 1, SkinID = 2, RedemptionDate = DateTime.UtcNow, IsEquipped = false };
        
        context.UserSkins.AddRange(userSkin1, userSkin2);
        await context.SaveChangesAsync();
        
        // Act - Equip skin 2, should unequip skin 1
        userSkin1.IsEquipped = false;
        userSkin2.IsEquipped = true;
        await context.SaveChangesAsync();
        
        // Assert
        var equippedSkins = await context.UserSkins
            .Where(us => us.UserID == 1 && us.IsEquipped)
            .ToListAsync();
            
        equippedSkins.Should().HaveCount(1);
        equippedSkins[0].SkinID.Should().Be(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task UserVoucher_CanBeRedeemed()
    {
        // Arrange
        var options = CreateDbOptions("UserVoucher_Redeem_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();
        
        context.Levels.AddRange(
            new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" },
            new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250, LevelImageURL = "/images/levels/sapling.png" }
        );
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 2, // Level 2 user
            TotalCoins = 300,
            LastLoginDate = DateTime.UtcNow,
            UserRole = "Player",
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
        
        var voucher = new Voucher
        {
            VoucherID = 1,
            VoucherName = "Level 2 Voucher",
            Description = "Reward for reaching Level 2",
            MinRedemptionLevel = 2
        };
        
        context.Users.Add(user);
        context.Vouchers.Add(voucher);
        await context.SaveChangesAsync();
        
        // Act
        var userVoucher = new UserVoucher
        {
            UserVoucherID = 1,
            UserID = 1,
            VoucherID = 1,
            RedemptionCode = "LEVEL2REWARD",
            RedemptionDate = DateTime.UtcNow
        };
        
        context.UserVouchers.Add(userVoucher);
        await context.SaveChangesAsync();
        
        // Assert
        var redeemedVoucher = await context.UserVouchers
            .FirstOrDefaultAsync(uv => uv.UserID == 1 && uv.VoucherID == 1);
            
        redeemedVoucher.Should().NotBeNull();
        redeemedVoucher!.RedemptionDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
