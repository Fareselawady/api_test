using api_test.Entities;
using api_test.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace api_test.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Student> Students { get; set; }
        public DbSet<VisitorLog> VisitorLogs { get; set; }

        public DbSet<User> Users { get; set; }
        public DbSet<Drug> Drugs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>()
               .HasMany(u => u.Drugs)
               .WithOne(d => d.User)
               .HasForeignKey(d => d.UserId)
               .OnDelete(DeleteBehavior.Cascade);
        }

    }
}
