using EmployeeManagement.Client.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<Position> Positions => Set<Position>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Rovnaký zamestnanec (meno + priezvisko + dátum narodenia) sa nesmie opakovať
            modelBuilder.Entity<Employee>()
                .HasIndex(e => new { e.Name, e.Surname, e.BirthDate })
                .IsUnique();

            // Názov pozície sa nesmie opakovať
            modelBuilder.Entity<Position>()
                .HasIndex(p => p.PositionName)
                .IsUnique();
        }
    }
}