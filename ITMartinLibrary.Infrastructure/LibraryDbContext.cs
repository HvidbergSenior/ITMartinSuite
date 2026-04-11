using ITMartinLibrary.Domain;
using ITMartinLibrary.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLibrary.Infrastructure
{
    public class LibraryDbContext : DbContext
    {
        public LibraryDbContext(DbContextOptions<LibraryDbContext> options)
            : base(options)
        {
        }

        public DbSet<InventoryItem> Items => Set<InventoryItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InventoryItem>()
                .HasIndex(x => x.Barcode)
                .IsUnique();
        }
    }
}