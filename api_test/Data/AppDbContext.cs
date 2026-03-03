using api_test.Entities;
using api_test.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

            var dateOnlyConverter = new ValueConverter<DateOnly?, DateTime?>(
                d => d == null ? null : d.Value.ToDateTime(TimeOnly.MinValue),
                d => d == null ? null : DateOnly.FromDateTime(d.Value)
            );

            var timeOnlyConverter = new ValueConverter<TimeOnly?, TimeSpan?>(
                t => t == null ? null : t.Value.ToTimeSpan(),
                t => t == null ? null : TimeOnly.FromTimeSpan(t.Value)
            );
            modelBuilder.Entity<UserMedication>(entity =>
            {
                entity.Property(e => e.StartDate).HasConversion(dateOnlyConverter);
                entity.Property(e => e.EndDate).HasConversion(dateOnlyConverter);
                entity.Property(e => e.ExpiryDate).HasConversion(dateOnlyConverter);
                entity.Property(e => e.FirstDoseTime).HasConversion(timeOnlyConverter);
                entity.Property(e => e.IntervalHours).HasConversion(
                    v => v == null ? (double?)null : (double)v.Value,
                    v => v == null ? (int?)null : (int)v.Value
                );
            });

            modelBuilder.Entity<MedicationSchedule>(entity =>
            {
                entity.Property(e => e.UserMedicationId).HasColumnName("UserMedId");
                entity.Ignore(e => e.TakenAt);
                entity.Ignore(e => e.SnoozedUntil);
                entity.Ignore(e => e.Notes);
                entity.Property(e => e.NotificationTime).IsRequired(false);
            });

            modelBuilder.Entity<MedIngredientLink>()
            .HasOne(m => m.Medication)
            .WithMany(d => d.Ingredients)
            .HasForeignKey(m => m.Med_id)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MedIngredientLink>()
                .HasOne(m => m.Ingredient)
                .WithMany(i => i.MedLinks)
                .HasForeignKey(m => m.Ingredient_id)
                .OnDelete(DeleteBehavior.Cascade);

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
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserMedication>()
    .HasOne(um => um.Medication)
    .WithMany(m => m.UserMedications)
    .HasForeignKey(um => um.MedId) 
    .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<MedicationSchedule>()
                .HasOne(ms => ms.UserMedication)
                .WithMany(um => um.MedicationSchedules)
                .HasForeignKey(ms => ms.UserMedicationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Alert>()
                .HasOne(a => a.MedicationSchedule)
                .WithMany(ms => ms.Alerts)
                .HasForeignKey(a => a.MedicationScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        }
}
