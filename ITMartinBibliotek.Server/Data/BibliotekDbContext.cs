using ITMartinBibliotek.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBibliotek.Server.Data;

public sealed class BibliotekDbContext(DbContextOptions<BibliotekDbContext> options) : DbContext(options)
{
    public DbSet<MediaItem> Items => Set<MediaItem>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MediaItem>().HasIndex(i => i.Barcode);
        b.Entity<MediaItem>().HasIndex(i => i.JellyfinId);
        b.Entity<Setting>().HasKey(s => s.Key);
    }
}
