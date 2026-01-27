using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Security.Claims;

namespace ADproject.Models.Entities
{
    public class MyDbContext : DbContext
    {

        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {
        }

        public MyDbContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseMySql(
                    "server=localhost;user=root;password=password;database=pocketree_db;",
                    new MySqlServerVersion(new Version(8, 0, 43))
                );
                
                optionsBuilder.UseLazyLoadingProxies();
            }
        }

        // Tables
        public DbSet<User> Users { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
        public DbSet<Task> Tasks { get; set; }
        public DbSet<Level> Levels { get; set; }
        public DbSet<UserTaskHistory> UserTaskHistory { get; set; }
        public DbSet<Badge> Badges { get; set; }
        public DbSet<GlobalMission> GlobalMissions { get; set; }
        public DbSet<Skin> Skins { get; set; }
        public DbSet<UserSkin> UserSkins { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<UserVoucher> UserVouchers { get; set; }
    }
}