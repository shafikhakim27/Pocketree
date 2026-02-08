using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using ADproject.Models.Entities;
using Testcontainers.MySql;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Pocketree.Api.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<global::Program>, IAsyncLifetime
{
    private readonly bool _useTestcontainers;
    private MySqlContainer? _dbContainer;

    public TestWebApplicationFactory()
    {
        _useTestcontainers = !string.Equals(
            Environment.GetEnvironmentVariable("USE_TESTCONTAINERS"),
            "false",
            StringComparison.OrdinalIgnoreCase);

        if (_useTestcontainers)
        {
            _dbContainer = new MySqlBuilder()
                .WithImage("mysql:8.0")
                .WithDatabase("pocketree_test")
                .WithUsername("testuser")
                .WithPassword("testpass")
                .WithCommand("--default-authentication-plugin=mysql_native_password")
                .Build();
        }
    }

    public async Task InitializeAsync()
    {
        if (_useTestcontainers && _dbContainer != null)
        {
            await _dbContainer.StartAsync();
        }
    }

    public new async Task DisposeAsync()
    {
        if (_useTestcontainers && _dbContainer != null)
        {
            await _dbContainer.DisposeAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing to skip production DB initialization
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registrations (MySql)
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<MyDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(MyDbContext) ||
                d.ServiceType.Name.Contains("EntityFrameworkCore") ||
                d.ServiceType.Name.Contains("DatabaseProvider")).ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            if (_useTestcontainers)
            {
                services.AddDbContext<MyDbContext>(options =>
                {
                    var connectionString = _dbContainer!.GetConnectionString();
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                        .UseLazyLoadingProxies()
                        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });
            }
            else
            {
                services.AddDbContext<MyDbContext>(options =>
                {
                    options.UseInMemoryDatabase("IntegrationTestDb")
                        .UseLazyLoadingProxies()
                        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });
            }
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        if (_useTestcontainers)
        {
            db.Database.Migrate();
        }
        else
        {
            db.Database.EnsureCreated();
        }

        if (!db.Users.Any())
        {
            db.Levels.Add(new Level
            {
                LevelID = 1,
                LevelName = "Seedling",
                MinCoins = 0,
                LevelImageURL = "/images/levels/seedling.png"
            });

            var testUser = new User
            {
                UserID = 1,
                Username = "testuser",
                Email = "test@test.com",
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
            };
            testUser.PasswordHash = passwordHasher.HashPassword(testUser, "Password123!");

            var adminUser = new User
            {
                UserID = 2,
                Username = "admin",
                Email = "admin@test.com",
                ProfileImageURL = "/images/default-user.jpg",
                CurrentLevelID = 1,
                TotalCoins = 0,
                LastLoginDate = DateTime.UtcNow,
                LastActivityDate = null,
                UserRole = "Admin",
                IsOnline = false,
                ResetCode = "",
                ResetExpiry = default(DateTime),
                UncompletedTaskCount = 0,
                NotAttemptedTaskCount = 0,
                FailedVerificationCount = 0
            };
            adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Admin123!");

            db.Users.AddRange(testUser, adminUser);
            db.SaveChanges();
        }

        return host;
    }
}