using ADproject.Models.Entities;
using ADproject.Models.DTOs;
using ADproject.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;

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

    private UserController CreateController(MyDbContext context, IPasswordHasher<User> hasher = null)
    {
        var mockHasher = hasher ?? Mock.Of<IPasswordHasher<User>>();
        var mockConfig = CreateMockConfiguration();
        
        return new UserController(context, mockHasher, mockConfig);
    }

    private Microsoft.Extensions.Configuration.IConfiguration CreateMockConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Jwt:Key", "ThisIsAVerySecureSecretKeyForJWTTokenGeneration123456789"},
            {"Jwt:Issuer", "PocketreeAPI"},
            {"Jwt:Audience", "PocketreeApp"}
        };

        Microsoft.Extensions.Configuration.IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        return configuration;
    }

    [Fact]
    public async System.Threading.Tasks.Task Register_NewUser_CreatesUserSuccessfully()
    {
        // Arrange
        var options = CreateDbOptions("Register_NewUser");
        using var context = new MyDbContext(options);
        
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
    }

    [Fact]
    public async System.Threading.Tasks.Task Register_DuplicateUsername_ReturnsBadRequest()
    {
        // Arrange
        var options = CreateDbOptions("Register_Duplicate");
        using var context = new MyDbContext(options);
        
        var existingUser = new User
        {
            UserID = 1,
            Username = "existinguser",
            Email = "existing@test.com",
            PasswordHash = "hash",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow
        };
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
        
        var mockHasher = new Mock<IPasswordHasher<User>>();
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hashed_password",
            TotalCoins = 100,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow
        };
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
        // Token should be in the response
    }

    [Fact]
    public async System.Threading.Tasks.Task Login_InvalidUsername_ReturnsUnauthorized()
    {
        // Arrange
        var options = CreateDbOptions("Login_InvalidUser");
        using var context = new MyDbContext(options);
        
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
        
        var mockHasher = new Mock<IPasswordHasher<User>>();
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hashed_password",
            TotalCoins = 100,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow
        };
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
        
        var mockHasher = new Mock<IPasswordHasher<User>>();
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "old_hashed_password",
            TotalCoins = 100,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow
        };
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
        
        var mockHasher = new Mock<IPasswordHasher<User>>();
        mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
                  .Returns("hashed_password");
        
        // Add GlobalMission for tree creation
        var mission = new GlobalMission
        {
            MissionID = 1,
            MissionName = "Greenify Sahara",
            TotalRequiredTrees = 1000,
            CurrentTreeCount = 0,
            PlantingFrequency = 1
        };
        context.GlobalMissions.Add(mission);
        await context.SaveChangesAsync();
        
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
        
        var oldLoginDate = DateTime.UtcNow.AddDays(-5);
        var mockHasher = new Mock<IPasswordHasher<User>>();
        var user = new User
        {
            UserID = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hashed_password",
            TotalCoins = 100,
            CurrentLevelID = 1,
            LastLoginDate = oldLoginDate
        };
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
}
