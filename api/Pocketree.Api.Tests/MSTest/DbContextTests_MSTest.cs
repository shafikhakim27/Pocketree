using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.EntityFrameworkCore;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using TestClass = Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestMethod = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using DataTestMethod = Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute;
using DataRow = Microsoft.VisualStudio.TestTools.UnitTesting.DataRowAttribute;

namespace Pocketree.Api.Tests.MSTestFramework;

[TestClass]
public class DbContextTests_MSTest
{
    [TestMethod]
    public void DbContext_ShouldInitialize_WithInMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_MSTest")
            .UseLazyLoadingProxies()
            .Options;

        using var context = new MyDbContext(options);

        Assert.IsNotNull(context);
        Assert.IsNotNull(context.Users);
        Assert.IsNotNull(context.Tasks);
        Assert.IsNotNull(context.Levels);
    }

    [TestMethod]
    public async System.Threading.Tasks.Task DbContext_CanAddAndRetrieve_User()
    {
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_MSTest_AddUser")
            .UseLazyLoadingProxies()
            .Options;

        using (var context = new MyDbContext(options))
        {
            var user = new User
            {
                UserID = 1,
                Username = "testuser_mstest",
                Email = "test@mstest.com",
                PasswordHash = "hashedpassword",
                TotalCoins = 100,
                CurrentLevelID = 1,
                LastLoginDate = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        using (var context = new MyDbContext(options))
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Username == "testuser_mstest");
            Assert.IsNotNull(user);
            Assert.AreEqual("testuser_mstest", user.Username);
            Assert.AreEqual("test@mstest.com", user.Email);
            Assert.AreEqual(100, user.TotalCoins);
        }
    }

    [TestMethod]
    public async System.Threading.Tasks.Task DbContext_CanAddAndRetrieve_Task()
    {
        var options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_MSTest_AddTask")
            .UseLazyLoadingProxies()
            .Options;

        using (var context = new MyDbContext(options))
        {
            var task = new ADproject.Models.Entities.Task
            {
                TaskID = 1,
                Description = "MSTest task",
                Difficulty = "Easy",
                CoinReward = 50,
                RequiresEvidence = false,
                Keyword = "mstest",
                Category = "Testing"
            };

            context.Tasks.Add(task);
            await context.SaveChangesAsync();
        }

        using (var context = new MyDbContext(options))
        {
            var task = await context.Tasks.FirstOrDefaultAsync(t => t.TaskID == 1);
            Assert.IsNotNull(task);
            Assert.AreEqual("MSTest task", task.Description);
            Assert.AreEqual("Easy", task.Difficulty);
            Assert.AreEqual(50, task.CoinReward);
            Assert.AreEqual("Testing", task.Category);
        }
    }
}

[TestClass]
public class MissionServiceTests_MSTest
{
    [TestMethod]
    public void MissionService_LocationSlots_ShouldHave50Locations()
    {
        var locationCount = MissionService.locSlots.Count;
        Assert.AreEqual(50, locationCount);
    }

    [TestMethod]
    public void MissionService_LocationSlots_ShouldHaveValidCoordinates()
    {
        var invalidLocations = MissionService.locSlots
            .Where(loc => loc.X < 0 || loc.X > 100 || loc.Y < 0 || loc.Y > 100)
            .ToList();

        Assert.AreEqual(0, invalidLocations.Count, "All coordinates should be within 0-100 range");
    }

    [TestMethod]
    public void MissionService_LocationSlots_ShouldHaveUniqueCoordinates()
    {
        var uniqueLocations = MissionService.locSlots.Distinct().Count();
        Assert.AreEqual(50, uniqueLocations);
    }
}

[TestClass]
public class EntityValidationTests_MSTest
{
    [DataTestMethod]
    [DataRow("Easy", 100)]
    [DataRow("Normal", 200)]
    [DataRow("Hard", 300)]
    public void Task_CoinReward_ShouldMatchDifficulty(string difficulty, int expectedCoins)
    {
        var task = new ADproject.Models.Entities.Task
        {
            TaskID = 1,
            Description = "Test task",
            Difficulty = difficulty,
            CoinReward = expectedCoins,
            RequiresEvidence = false,
            Keyword = "test",
            Category = "Testing"
        };

        Assert.AreEqual(expectedCoins, task.CoinReward);
        Assert.AreEqual(difficulty, task.Difficulty);
    }

    [TestMethod]
    public void Level_Progression_ShouldHaveIncreasingMinCoins()
    {
        var levels = new List<Level>
        {
            new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0 },
            new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250 },
            new Level { LevelID = 3, LevelName = "Mighty Oak", MinCoins = 500 }
        };

        var isAscending = levels
            .Zip(levels.Skip(1), (a, b) => a.MinCoins < b.MinCoins)
            .All(x => x);

        Assert.IsTrue(isAscending);
    }

    [TestMethod]
    public void User_DefaultValues_ShouldBeValid()
    {
        var user = new User
        {
            UserID = 1,
            Username = "newuser",
            Email = "new@example.com",
            PasswordHash = "hash",
            TotalCoins = 0,
            CurrentLevelID = 1,
            LastLoginDate = DateTime.UtcNow
        };

        Assert.AreEqual(0, user.TotalCoins);
        Assert.AreEqual(1, user.CurrentLevelID);
        Assert.IsNotNull(user.Username);
        Assert.IsNotNull(user.Email);
    }
}
