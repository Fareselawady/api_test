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

        public DbSet<VisitorLog> VisitorLogs { get; set; }
        public DbSet<MedicationSchedule> MedicationSchedules { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }  
        public DbSet<UserMedication> UserMedications { get; set; }
        public DbSet<Medication> Medications { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<MedIngredientLink> Med_Ingredients_Link { get; set; }
        public DbSet<DrugInteraction> Drug_Interactions { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MedIngredientLink>()
            .HasOne(m => m.Medication)
            .WithMany(d => d.Ingredients)
            .HasForeignKey(m => m.Med_id);

            modelBuilder.Entity<MedIngredientLink>()
                .HasOne(m => m.Ingredient)
                .WithMany(i => i.MedLinks)
                .HasForeignKey(m => m.Ingredient_id);

            modelBuilder.Entity<DrugInteraction>()
                .HasOne(d => d.Ingredient1)
                .WithMany()
                .HasForeignKey(d => d.Ingredient_1_id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DrugInteraction>()
                .HasOne(d => d.Ingredient2)
                .WithMany()
                .HasForeignKey(d => d.Ingredient_2_id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
               .HasMany(u => u.UserMedications)
               .WithOne(d => d.User)
               .HasForeignKey(d => d.UserId)
               .OnDelete(DeleteBehavior.Cascade);

            
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

          
            modelBuilder.Entity<Role>().HasData(
                new Role { RoleId = 1, RoleName = "Admin" },
                new Role { RoleId = 2, RoleName = "Patient" }
            );

            modelBuilder.Entity<Alert>()
             .HasOne(a => a.User)
             .WithMany(u => u.Alerts)
            .HasForeignKey(a => a.UserId)
           .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Alert>()
                .HasOne(a => a.UserMedication)
                .WithMany(m => m.Alerts)
                .HasForeignKey(a => a.UserMedicationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Alert>()
                .HasOne(a => a.MedicationSchedule)
                .WithMany(s => s.Alerts)
                .HasForeignKey(a => a.MedicationScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserMedication>()
    .HasOne(um => um.Medication)
    .WithMany(m => m.UserMedications)
    .HasForeignKey(um => um.MedId) 
    .OnDelete(DeleteBehavior.Cascade);
        }

        }
}
