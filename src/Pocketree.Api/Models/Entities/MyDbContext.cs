using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Security.Claims;

namespace ADproject.Models.Entities
{
    public class MyDbContext : DbContext
    {
        // 1. THIS CONSTRUCTOR IS CRITICAL
        // It accepts the "Retry Logic" and "Connection String" from Program.cs
        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {
        }

        // 2. WE REMOVED 'OnConfiguring'
        // This stops the file from forcing a "localhost" connection that breaks Docker.

        // Tables
        public DbSet<User> Users { get; set; }
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