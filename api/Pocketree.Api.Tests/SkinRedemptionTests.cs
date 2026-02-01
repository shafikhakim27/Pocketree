using ADproject.Models.Entities;
using ADproject.Controllers;
using ADproject.Services;
using ADproject.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Moq;
using System.Security.Claims;

namespace Pocketree.Api.Tests;

public class SkinRedemptionTests
{
    private DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private TaskController CreateController(MyDbContext context)
    {
        var mockMlService = Mock.Of<IMlService>();
        var mockHubContext = new Mock<IHubContext<MapHub>>();
        var missionService = new MissionService(context, mockHubContext.Object);
        
        var controller = new TaskController(context, mockMlService, missionService);
        
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.Name, "testuser")
        }, "mock"));
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        
        return controller;
    }

    [Fact]
    public async System.Threading.Tasks.Task SkinRedemption_SufficientCoins_RedeemsSuccessfully()
    {
        // Arrange
        var options = CreateDbOptions("Skin_SufficientCoins");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 500,
            CurrentLevelID = 2,
            LastLoginDate = DateTime.UtcNow
        };
        
        var skin = new Skin
        {
            SkinID = 1,
            SkinName = "Golden Tree",
            ImageURL = "golden_tree.png",
            SkinPrice = 300
        };
        
        context.Users.Add(user);
        context.Skins.Add(skin);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RedeemSkinApi(1);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedUser = await context.Users.FindAsync(1);
        Assert.Equal(200, updatedUser.TotalCoins); // 500 - 300 = 200
        
        var userSkin = await context.UserSkins.FirstOrDefaultAsync(us => us.UserID == 1 && us.SkinID == 1);
        Assert.NotNull(userSkin);
        Assert.True(userSkin.IsEquipped);
    }

    [Fact]
    public async System.Threading.Tasks.Task SkinRedemption_InsufficientCoins_ReturnsBadRequest()
    {
        // Arrange
        var options = CreateDbOptions("Skin_InsufficientCoins");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 100, // Not enough for 300-coin skin
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow
        };
        
        var skin = new Skin
        {
            SkinID = 1,
            SkinName = "Expensive Tree",
            ImageURL = "expensive_tree.png",
            SkinPrice = 300
        };
        
        context.Users.Add(user);
        context.Skins.Add(skin);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RedeemSkinApi(1);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Insufficient coins.", badRequestResult.Value);
    }

    [Fact]
    public async System.Threading.Tasks.Task SkinRedemption_InvalidSkin_ReturnsBadRequest()
    {
        // Arrange
        var options = CreateDbOptions("Skin_InvalidSkin");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 500,
            CurrentLevelID = 2,
            LastLoginDate = DateTime.UtcNow
        };
        
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RedeemSkinApi(999); // Non-existent skin
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Requested skin cannot be found.", badRequestResult.Value);
    }

    [Fact]
    public async System.Threading.Tasks.Task SkinEquip_OwnedSkin_EquipsSuccessfully()
    {
        // Arrange
        var options = CreateDbOptions("Skin_Equip");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 500,
            CurrentLevelID = 2,
            LastLoginDate = DateTime.UtcNow
        };
        
        var skin = new Skin
        {
            SkinID = 1,
            SkinName = "Cool Tree",
            ImageURL = "cool_tree.png",
            SkinPrice = 100
        };
        
        var userSkin = new UserSkin
        {
            UserSkinID = 1,
            UserID = 1,
            SkinID = 1,
            RedemptionDate = DateTime.UtcNow,
            IsEquipped = false // Not currently equipped
        };
        
        context.Users.Add(user);
        context.Skins.Add(skin);
        context.UserSkins.Add(userSkin);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.EquipSkinApi(1);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedUserSkin = await context.UserSkins.FindAsync(1);
        Assert.True(updatedUserSkin.IsEquipped);
    }

    [Fact]
    public async System.Threading.Tasks.Task SkinEquip_NotOwnedSkin_ReturnsBadRequest()
    {
        // Arrange
        var options = CreateDbOptions("Skin_EquipNotOwned");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 500,
            CurrentLevelID = 2,
            LastLoginDate = DateTime.UtcNow
        };
        
        var skin = new Skin
        {
            SkinID = 1,
            SkinName = "Unowned Tree",
            ImageURL = "unowned_tree.png",
            SkinPrice = 100
        };
        
        context.Users.Add(user);
        context.Skins.Add(skin);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.EquipSkinApi(1);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("You do not own this skin.", badRequestResult.Value);
    }

    [Fact]
    public async System.Threading.Tasks.Task SkinRedemption_ExactCoins_RedeemsSuccessfully()
    {
        // Arrange
        var options = CreateDbOptions("Skin_ExactCoins");
        using var context = new MyDbContext(options);
        
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash",
            TotalCoins = 300, // Exactly the skin price
            CurrentLevelID = 2,
            LastLoginDate = DateTime.UtcNow
        };
        
        var skin = new Skin
        {
            SkinID = 1,
            SkinName = "Exact Price Tree",
            ImageURL = "exact_price_tree.png",
            SkinPrice = 300
        };
        
        context.Users.Add(user);
        context.Skins.Add(skin);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        // Act
        var result = await controller.RedeemSkinApi(1);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedUser = await context.Users.FindAsync(1);
        Assert.Equal(0, updatedUser.TotalCoins); // Should have 0 coins left
    }
}
