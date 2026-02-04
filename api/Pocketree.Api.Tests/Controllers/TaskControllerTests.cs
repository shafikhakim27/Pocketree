using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;
using TaskEntity = ADproject.Models.Entities.Task;

namespace Pocketree.Api.Tests.Controllers;

public class TaskControllerTests
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

    [Fact]
    public async System.Threading.Tasks.Task GetDailyTasksApi_AssignsTasksAndUpdatesHistory()
    {
        var options = CreateDbOptions("TaskController_GetDaily_" + Guid.NewGuid());
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
            TotalCoins = 0,
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

        context.UserSettings.Add(new UserSettings
        {
            UserID = 1,
            UseMlRecommendation = true
        });

        await context.SaveChangesAsync();

        var mockMl = new Mock<IMlService>();
        mockMl.Setup(m => m.GetRecommendedTasks(1)).ReturnsAsync(new List<TaskEntity>
        {
            new TaskEntity { TaskID = 11, Description = "Easy", Difficulty = "Easy", CoinReward = 10, RequiresEvidence = false, Keyword = "easy", Category = "Test" },
            new TaskEntity { TaskID = 12, Description = "Normal", Difficulty = "Normal", CoinReward = 20, RequiresEvidence = false, Keyword = "normal", Category = "Test" },
            new TaskEntity { TaskID = 13, Description = "Hard", Difficulty = "Hard", CoinReward = 30, RequiresEvidence = true, Keyword = "hard", Category = "Test" }
        });

        var mockHub = new Mock<IHubContext<MapHub>>();
        var missionService = new MissionService(context, mockHub.Object);

        var controller = new ADproject.Controllers.TaskController(context, mockMl.Object, missionService);
        SetUser(controller, "testuser");

        var result = await controller.GetDailyTasksApi();

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var tasks = ok.Value as IEnumerable<TaskEntity>;
        tasks.Should().NotBeNull();
        tasks!.Should().HaveCount(3);

        var history = await context.UserTaskHistory.ToListAsync();
        history.Should().HaveCount(3);

        var user = await context.Users.FindAsync(1);
        user!.UncompletedTaskCount.Should().Be(3);
    }

    [Fact]
    public async System.Threading.Tasks.Task RedeemSkinApi_AddsUserSkinAndDeductsCoins()
    {
        var options = CreateDbOptions("TaskController_RedeemSkin_" + Guid.NewGuid());
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

        context.Skins.Add(new Skin
        {
            SkinID = 5,
            SkinName = "Forest",
            SkinPrice = 40,
            ImageURL = "forest.png",
            SkinKey = "forest"
        });

        await context.SaveChangesAsync();

        var mockMl = new Mock<IMlService>();
        var mockHub = new Mock<IHubContext<MapHub>>();
        var missionService = new MissionService(context, mockHub.Object);

        var controller = new ADproject.Controllers.TaskController(context, mockMl.Object, missionService);
        SetUser(controller, "testuser");

        var result = await controller.RedeemSkinApi(5);

        result.Should().BeOfType<OkObjectResult>();

        var user = await context.Users.FindAsync(1);
        user!.TotalCoins.Should().Be(60);

        var userSkin = await context.UserSkins.FirstOrDefaultAsync(us => us.UserID == 1 && us.SkinID == 5);
        userSkin.Should().NotBeNull();
        userSkin!.IsRedeemed.Should().BeTrue();
    }

    [Fact]
    public async System.Threading.Tasks.Task EquipSkinApi_EquipsSelectedAndUnequipsOthers()
    {
        var options = CreateDbOptions("TaskController_EquipSkin_" + Guid.NewGuid());
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
            new Skin { SkinID = 1, SkinName = "A", SkinPrice = 10, SkinKey = "a" },
            new Skin { SkinID = 2, SkinName = "B", SkinPrice = 10, SkinKey = "b" }
        );

        context.UserSkins.AddRange(
            new UserSkin { UserID = 1, SkinID = 1, IsEquipped = true, IsRedeemed = true, RedemptionDate = DateTime.UtcNow },
            new UserSkin { UserID = 1, SkinID = 2, IsEquipped = false, IsRedeemed = true, RedemptionDate = DateTime.UtcNow }
        );

        await context.SaveChangesAsync();

        var mockMl = new Mock<IMlService>();
        var mockHub = new Mock<IHubContext<MapHub>>();
        var missionService = new MissionService(context, mockHub.Object);

        var controller = new ADproject.Controllers.TaskController(context, mockMl.Object, missionService);
        SetUser(controller, "testuser");

        var result = await controller.EquipSkinApi(2);

        result.Should().BeOfType<OkObjectResult>();

        var updated = await context.UserSkins.Where(us => us.UserID == 1).ToListAsync();
        updated.Single(us => us.SkinID == 1).IsEquipped.Should().BeFalse();
        updated.Single(us => us.SkinID == 2).IsEquipped.Should().BeTrue();
    }
}
