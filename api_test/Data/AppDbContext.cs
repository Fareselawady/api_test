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
    }
}
