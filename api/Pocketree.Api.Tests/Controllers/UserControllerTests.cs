using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Pocketree.Api.Models.DTOs;
using Pocketree.Api.Models.ViewModels;
using System.Security.Claims;

namespace Pocketree.Api.Tests.Controllers;

public class UserControllerTests
{
    private DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private static void SetUser(ControllerBase controller, string username)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private static IConfiguration CreateConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "TestSecretKey123456789012345678901234567890",
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience",
            ["StorageBaseURL"] = "https://cdn.test"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public async System.Threading.Tasks.Task RegisterApi_CreatesUserAndPlantsSeed()
    {
        var options = CreateDbOptions("UserController_Register_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        if (!context.GlobalMissions.Any())
        {
            context.GlobalMissions.Add(new GlobalMission
            {
                MissionID = 1,
                MissionName = "Greenify Sahara",
                TotalRequiredTrees = 10,
                CurrentTreeCount = 0,
                PlantingFrequency = 1
            });
            await context.SaveChangesAsync();
        }

        var mockHasher = new Mock<IPasswordHasher<User>>();
        mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>())).Returns("hash");

        var controller = new ADproject.Controllers.UserController(context, mockHasher.Object, CreateConfiguration());

        var result = await controller.RegisterApi(new UserRegistrationDto
        {
            Username = "newuser",
            Email = "new@test.com",
            Password = "Password123!"
        });

        result.Should().BeOfType<OkObjectResult>();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == "newuser");
        user.Should().NotBeNull();
        user!.ProfileImageURL.Should().Be("https://cdn.test/images/default-user.jpg");

        var tree = await context.Trees.FirstOrDefaultAsync(t => t.UserID == user.UserID);
        tree.Should().NotBeNull();
        tree!.MissionID.Should().Be(1);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetSkinsShopApi_ReturnsRedeemedAndEquippedFlags()
    {
        var options = CreateDbOptions("UserController_SkinShop_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Users.Add(new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1,
            TotalCoins = 100,
            LastLoginDate = DateTime.UtcNow,
            LastActivityDate = null,
            UserRole = "Player",
            IsOnline = false,
            ResetCode = "",
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        });

        context.Skins.AddRange(
            new Skin { SkinID = 1, SkinName = "A", SkinPrice = 10, ImageURL = "a.png", SkinKey = "a" },
            new Skin { SkinID = 2, SkinName = "B", SkinPrice = 20, ImageURL = "b.png", SkinKey = "b" }
        );

        context.UserSkins.Add(new UserSkin
        {
            UserID = 1,
            SkinID = 2,
            IsRedeemed = true,
            IsEquipped = true,
            RedemptionDate = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var mockHasher = new Mock<IPasswordHasher<User>>();
        var controller = new ADproject.Controllers.UserController(context, mockHasher.Object, CreateConfiguration());
        SetUser(controller, "testuser");

        var result = await controller.GetSkinsShopApi();

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var shopList = ok.Value as IEnumerable<SkinShopDto>;
        shopList.Should().NotBeNull();

        var items = shopList!.ToList();
        items.Should().HaveCount(2);

        var skinA = items.Single(i => i.SkinID == 1);
        skinA.IsRedeemed.Should().BeFalse();
        skinA.IsEquipped.Should().BeFalse();

        var skinB = items.Single(i => i.SkinID == 2);
        skinB.IsRedeemed.Should().BeTrue();
        skinB.IsEquipped.Should().BeTrue();
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAllSkinsOfferedApi_ReturnsBaseUrlImages()
    {
        var options = CreateDbOptions("UserController_AllSkins_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Skins.Add(new Skin
        {
            SkinID = 1,
            SkinName = "Forest",
            SkinPrice = 10,
            ImageURL = "/forest.png",
            SkinKey = "forest"
        });
        await context.SaveChangesAsync();

        var mockHasher = new Mock<IPasswordHasher<User>>();
        var controller = new ADproject.Controllers.UserController(context, mockHasher.Object, CreateConfiguration());

        var result = await controller.GetAllSkinsOfferedApi();

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var items = ok.Value as IEnumerable<object>;
        items.Should().NotBeNull();
        items!.Should().HaveCount(1);

        var json = System.Text.Json.JsonSerializer.Serialize(items);
        json.Should().Contain("https://cdn.test/forest.png");
    }
}
