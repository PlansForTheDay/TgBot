using botTelegram.Models;
using Microsoft.EntityFrameworkCore;

namespace botTelegram.DateBase
{
    public class BeerDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Presence> Presences { get; set; }

        public BeerDbContext()
        {
            Database.EnsureCreated();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=Datebase.db").EnableSensitiveDataLogging();
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Presence>().HasKey(u => new { u.IdUser, u.IdEvent });
        }
    }
}
