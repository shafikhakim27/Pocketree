using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using ADproject.Models.Entities;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace Pocketree.Api.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ✅ Set environment to Testing to skip database initialization
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // ✅ CRITICAL: Remove ALL DbContext-related services AND EntityFrameworkCore services
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

            // ✅ Add ONLY InMemory database for testing
            services.AddDbContext<MyDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDb")
                    .UseLazyLoadingProxies()
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        
        // Seed the database AFTER host is built
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        try
        {
            db.Database.EnsureCreated();
        }
        catch
        {
            // InMemory database - no need to ensure created
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
