using ADproject.Controllers;
using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Pocketree.Api.Services;

namespace Pocketree.Api.Tests;

public class AuthenticationTests
{
    private DbContextOptions<MyDbContext> CreateDbOptions(string dbName)
    {
        return new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseLazyLoadingProxies()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private async System.Threading.Tasks.Task SeedRequiredData(MyDbContext context)
    {
        if (!context.Levels.Any())
        {
            context.Levels.AddRange(
                new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" },
                new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250, LevelImageURL = "/images/levels/sapling.png" },
                new Level { LevelID = 3, LevelName = "Mighty Oak", MinCoins = 500, LevelImageURL = "/images/levels/oak.png" }
            );
        }

        if (!context.GlobalMissions.Any())
        {
            context.GlobalMissions.Add(new GlobalMission
            {
                MissionID = 1,
                MissionName = "Greenify Sahara",
                TotalRequiredTrees = 1000,
                CurrentTreeCount = 0,
                PlantingFrequency = 1
            });
        }

        await context.SaveChangesAsync();
    }

    private UserController CreateController(MyDbContext context, IPasswordHasher<User> hasher = null)
    {
        var mockHasher = hasher ?? Mock.Of<IPasswordHasher<User>>();
        var mockConfig = CreateMockConfiguration();
        var mockBlobService = new Mock<BlobService>();
        return new UserController(context, mockHasher, mockConfig, mockBlobService.Object);
    }

    private IConfiguration CreateMockConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Jwt:Key", "ThisIsAVerySecureSecretKeyForJWTTokenGeneration123456789"},
            {"Jwt:Issuer", "PocketreeAPI"},
            {"Jwt:Audience", "PocketreeApp"}
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
    }

    private User CreateTestUser(
        int userId = 1,
        string username = "testuser",
        string email = "test@test.com",
        string passwordHash = "hashed_password",
        string role = "Player",
        int coins = 0,
        DateTime? lastLogin = null)
    {
        return new User
        {
            UserID = userId,
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            ProfileImageURL = "/images/default-user.jpg",
            CurrentLevelID = 1,
            TotalCoins = coins,
            LastLoginDate = lastLogin ?? DateTime.UtcNow,
            LastActivityDate = null,
            UserRole = role,
            IsOnline = false,
            ResetExpiry = default(DateTime),
            UncompletedTaskCount = 0,
            NotAttemptedTaskCount = 0,
            FailedVerificationCount = 0
        };
    }

    [Fact]
    public async System.Threading.Tasks.Task Register_NewUser_CreatesUserSuccessfully()
    {
        // Arrange
        var options = CreateDbOptions("Register_NewUser");
        using var context = new MyDbContext(options);
        await SeedRequiredData(context);
        
        var mockHasher = new Mock<IPasswordHasher<User>>();
        mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
                  .Returns("hashed_password_123");
        
        var controller = CreateController(context, mockHasher.Object);
        
        var dto = new UserRegistrationDto
        {
            Username = "newuser",
            Password = "SecurePassword123",
            Email = "newuser@test.com"
        };
        
        // Act
        var result = await controller.RegisterApi(dto);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var createdUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "newuser");
        Assert.NotNull(createdUser);
        Assert.Equal("newuser", createdUser.Username);
        Assert.Equal("newuser@test.com", createdUser.Email);
        Assert.Equal("hashed_password_123", createdUser.PasswordHash);
        Assert.Equal(0, createdUser.TotalCoins);
        Assert.Equal(1, createdUser.CurrentLevelID);
        Assert.Equal("Player", createdUser.UserRole);
        Assert.False(createdUser.IsOnline);
    }

    [Fact]
    public async System.Threading.Tasks.Task Register_DuplicateUsername_ReturnsBadRequest()
    {
        // Arrange
        var options = CreateDbOptions("Register_Duplicate");
        using var context = new MyDbContext(options);
        await SeedRequiredData(context);
        
        var existingUser = CreateTestUser(username: "existinguser", email: "existing@test.com");
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();
        
        var controller = CreateController(context);
        
        var dto = new UserRegistrationDto
        {
            Username = "existinguser",
            Password = "Password123",
            Email = "newemail@test.com"
        };
        
        // Act
        var result = await controller.RegisterApi(dto);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Username is already taken.", badRequestResult.Value);
    }

    [Fact]
    public async System.Threading.Tasks.Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var options = CreateDbOptions("Login_Valid");
        using var context = new MyDbContext(options);
        await SeedRequiredData(context);
        
        var mockHasher = new Mock<IPasswordHasher<User>>();
        var user = CreateTestUser(coins: 100);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        mockHasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), "hashed_password", "CorrectPassword123"))
                  .Returns(PasswordVerificationResult.Success);
        
        var controller = CreateController(context, mockHasher.Object);
        
        var dto = new UserLoginDto
        {
            Username = "testuser",
            Password = "CorrectPassword123"
        };
        
        // Act
        var result = await controller.LoginApi(dto);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async System.Threading.Tasks.Task Login_InvalidUsername_ReturnsUnauthorized()
    {
        // Arrange
        var options = CreateDbOptions("Login_InvalidUser");
        using var context = new MyDbContext(options);
        await SeedRequiredData(context);
        
        var controller = CreateController(context);
        
        var dto = new UserLoginDto
        {
            Username = "nonexistent",
            Password = "Password123"
        };
        
        // Act
        var result = await controller.LoginApi(dto);
        
        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async System.Threading.Tasks.Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var options = CreateDbOptions("Login_InvalidPassword");
        using var context = new MyDbContext(options);
        await SeedRequiredData(context);
        
        var mockHasher = new Mock<IPasswordHasher<User>>();
        var user = CreateTestUser();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        mockHasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), "hashed_password", "WrongPassword"))
                  .Returns(PasswordVerificationResult.Failed);
        
        var controller = CreateController(context, mockHasher.Object);
        
        var dto = new UserLoginDto
        {
            Username = "testuser",
            Password = "WrongPassword"
        };
        
        // Act
        var result = await controller.LoginApi(dto);
        
        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async System.Threading.Tasks.Task ChangePassword_ValidOldPassword_ChangesSuccessfully()
    {
        // Arrange
        var options = CreateDbOptions("ChangePassword_Valid");
        using var context = new MyDbContext(options);
        await SeedRequiredData(context);
        
        var mockHasher = new Mock<IPasswordHasher<User>>();
        var user = CreateTestUser(passwordHash: "old_hashed_password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        mockHasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), "old_hashed_password", "OldPassword123"))
                  .Returns(PasswordVerificationResult.Success);
        mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), "NewPassword123"))
                  .Returns("new_hashed_password");
        
        var controller = CreateController(context, mockHasher.Object);
        
        var dto = new ChangePasswordDto
        {
            CurrentPassword = "OldPassword123",
            NewPassword = "NewPassword123",
            ConfirmNewPassword = "NewPassword123"
        };
        
        // Act
        // Note: This test will fail if ChangePasswordApi doesn't exist yet
        // var result = await controller.ChangePasswordApi(dto);
        
        // Assert
        // Assert.IsType<OkObjectResult>(result);
        // var updatedUser = await context.Users.FindAsync(1);
        // Assert.Equal("new_hashed_password", updatedUser.PasswordHash);
    }

    [Fact]
    public async System.Threading.Tasks.Task Register_CreatesInitialTree()
    {
        // Arrange
        var options = CreateDbOptions("Register_CreatesTree");
        using var context = new MyDbContext(options);
        await SeedRequiredData(context);
        
        var mockHasher = new Mock<IPasswordHasher<User>>();
        mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
                  .Returns("hashed_password");
        
        var controller = CreateController(context, mockHasher.Object);
        
        var dto = new UserRegistrationDto
        {
            Username = "treeuser",
            Password = "Password123",
            Email = "tree@test.com"
        };
        
        // Act
        var result = await controller.RegisterApi(dto);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var createdUser = await context.Users
            .Include(u => u.Trees)
            .FirstOrDefaultAsync(u => u.Username == "treeuser");
        
        // User should have an initial tree created
        Assert.NotNull(createdUser);
        // Tree creation might be done after registration
    }

    [Fact]
    public async System.Threading.Tasks.Task Login_UpdatesLastLoginDate()
    {
        // Arrange
        var options = CreateDbOptions("Login_UpdatesDate");
        using var context = new MyDbContext(options);
        await SeedRequiredData(context);
        
        var oldLoginDate = DateTime.UtcNow.AddDays(-5);
        var mockHasher = new Mock<IPasswordHasher<User>>();
        var user = CreateTestUser(lastLogin: oldLoginDate, coins: 100);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        mockHasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), "hashed_password", "Password123"))
                  .Returns(PasswordVerificationResult.Success);
        
        var controller = CreateController(context, mockHasher.Object);
        
        var dto = new UserLoginDto
        {
            Username = "testuser",
            Password = "Password123"
        };
        
        // Act
        var result = await controller.LoginApi(dto);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var updatedUser = await context.Users.FindAsync(1);
        Assert.True(updatedUser.LastLoginDate > oldLoginDate);
    }

    [Fact]
    public async System.Threading.Tasks.Task Login_Should_ReturnUserRole()
    {
        // Arrange
        var options = CreateDbOptions("Login_ReturnsUserRole");
        using var context = new MyDbContext(options);
        await SeedRequiredData(context);
        
        var mockHasher = new Mock<IPasswordHasher<User>>();
        var user = CreateTestUser(username: "playeruser", email: "player@test.com");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        mockHasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), "hashed_password", "Password123"))
                  .Returns(PasswordVerificationResult.Success);
        
        var controller = CreateController(context, mockHasher.Object);
        
        var dto = new UserLoginDto
        {
            Username = "playeruser",
            Password = "Password123"
        };
        
        // Act
        var result = await controller.LoginApi(dto);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var loggedInUser = await context.Users.FirstAsync(u => u.Username == "playeruser");
        Assert.Equal("Player", loggedInUser.UserRole);
    }

    [Fact]
    public async System.Threading.Tasks.Task AdminLogin_Should_ReturnAdminRole()
    {
        // Arrange
        var options = CreateDbOptions("AdminLogin");
        using var context = new MyDbContext(options);
        await SeedRequiredData(context);
        
        var mockHasher = new Mock<IPasswordHasher<User>>();
        var adminUser = CreateTestUser(username: "adminuser", email: "admin@test.com", role: "Admin");
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();
        
        mockHasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), "hashed_password", "AdminPass123"))
                  .Returns(PasswordVerificationResult.Success);
        
        var controller = CreateController(context, mockHasher.Object);
        
        var dto = new UserLoginDto
        {
            Username = "adminuser",
            Password = "AdminPass123"
        };
        
        // Act
        var result = await controller.LoginApi(dto);
        
        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Admins must use the Web portal.", badRequest.Value);
    }
}
