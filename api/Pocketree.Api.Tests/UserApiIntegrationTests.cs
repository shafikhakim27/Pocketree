using ADproject.Controllers;
using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace Pocketree.Api.Tests.Integration;

public class UserApiIntegrationTests
{
    private DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
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
    public async System.Threading.Tasks.Task Login_Should_ReturnUserProfile_And_SetOnline()
    {
        var options = CreateDbOptions("User_Login_" + Guid.NewGuid());
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
            LastLoginDate = DateTime.UtcNow.AddDays(-1),
            LastActivityDate = null,
            UserRole = "Player",
            IsOnline = false,
            ResetCode = "",
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var mockHasher = new Mock<IPasswordHasher<User>>();
        mockHasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), "hash", "Password123!"))
            .Returns(PasswordVerificationResult.Success);

        var controller = new UserController(context, mockHasher.Object, CreateConfiguration());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost:5042");

        var result = await controller.LoginApi(new UserLoginDto
        {
            Username = "testuser",
            Password = "Password123!"
        });

        result.Should().BeOfType<OkObjectResult>();

        var updated = await context.Users.FindAsync(1);
        updated!.IsOnline.Should().BeTrue();
    }

    [Fact]
    public async System.Threading.Tasks.Task Register_Should_CreateUserAndSeedTree()
    {
        var options = CreateDbOptions("User_Register_" + Guid.NewGuid());
        using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        if (!context.GlobalMissions.Any())
        {
            context.GlobalMissions.Add(new GlobalMission
            {
                MissionID = 1,
                MissionName = "Greenify Sahara",
                TotalRequiredTrees = 100,
                CurrentTreeCount = 0,
                PlantingFrequency = 1
            });
        }
        await context.SaveChangesAsync();

        var mockHasher = new Mock<IPasswordHasher<User>>();
        mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>())).Returns("hash");

        var controller = new UserController(context, mockHasher.Object, CreateConfiguration());

        var result = await controller.RegisterApi(new UserRegistrationDto
        {
            Username = "newuser",
            Email = "newuser@test.com",
            Password = "SecurePass123!"
        });

        result.Should().BeOfType<OkObjectResult>();

        var created = await context.Users.FirstOrDefaultAsync(u => u.Username == "newuser");
        created.Should().NotBeNull();
        created!.UserRole.Should().Be("Player");

        var tree = await context.Trees.FirstOrDefaultAsync(t => t.UserID == created.UserID);
        tree.Should().NotBeNull();
    }
}
