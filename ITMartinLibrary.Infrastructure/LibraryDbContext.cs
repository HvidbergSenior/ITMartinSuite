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
        public DbSet<ScannedShelf> ScannedShelves => Set<ScannedShelf>();
        public DbSet<ShelfBook> ShelfBooks => Set<ShelfBook>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InventoryItem>()
                .HasIndex(x => x.Barcode)
                .IsUnique();

            modelBuilder.Entity<ScannedShelf>()
                .HasMany(x => x.Books)
                .WithOne(x => x.Shelf)
                .HasForeignKey(x => x.ScannedShelfId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}