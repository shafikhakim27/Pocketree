using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Security.Claims;

namespace ADproject.Models.Entities
{
    public class MyDbContext : DbContext
    {
      
        // Constructor for dependency injection
        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Only configure if not already configured (for EF migrations/tooling)
            if (!optionsBuilder.IsConfigured)
            {
                // Fallback connection for migrations - should not be used in runtime
                optionsBuilder.UseMySql(
                    "server=localhost;user=root;password=password;database=pocketree_db;",
                    new MySqlServerVersion(new Version(8, 0, 43))
                );
            }
            
            // Lazy loading is always enabled
            optionsBuilder.UseLazyLoadingProxies();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Prevents a GlobalMission from being deleted if users still have trees linked to it 
            modelBuilder.Entity<Tree>()
                .HasOne(t => t.GlobalMission)
                .WithMany(m => m.Trees)
                .HasForeignKey(t => t.MissionID)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed data for global mission
            modelBuilder.Entity<GlobalMission>().HasData(
                new GlobalMission
                {
                    MissionID = 1,
                    MissionName = "Greenify Sahara",
                    TotalRequiredTrees = 1000,
                    CurrentTreeCount = 0,
                    PlantingFrequency = 1
                }
            );
        }

        // tables
        public DbSet<User> Users { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
        public DbSet<Task> Tasks { get; set; }
        public DbSet<Level> Levels { get; set; }
        public DbSet<UserTaskHistory> UserTaskHistory { get; set; }
        public DbSet<Badge> Badges { get; set; }
        public DbSet<UserBadge> UserBadges { get; set; }
        public DbSet<GlobalMission> GlobalMissions { get; set; }
        public DbSet<Skin> Skins { get; set; }
        public DbSet<UserSkin> UserSkins { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<UserVoucher> UserVouchers { get; set; }
        public DbSet<CommunityForest> CommunityForests { get; set; }
        public DbSet<Tree> Trees { get; set; }
    }

}
