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
        public DbSet<LibraryGroup> Groups => Set<LibraryGroup>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Was a single global unique index on Barcode - now scoped per
            // group, since two tenants (e.g. Martin's personal collection and
            // Bogshoppen's stock) can otherwise scan the same ISBN.
            modelBuilder.Entity<InventoryItem>()
                .HasIndex(x => new { x.GroupId, x.Barcode })
                .IsUnique();

            modelBuilder.Entity<ScannedShelf>()
                .HasMany(x => x.Books)
                .WithOne(x => x.Shelf)
                .HasForeignKey(x => x.ScannedShelfId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LibraryGroup>()
                .HasIndex(x => x.Slug)
                .IsUnique();
        }
    }
}