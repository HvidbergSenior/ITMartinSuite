using ITMartinStrategi.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinStrategi.Server.Data;

public sealed class StrategiDbContext(DbContextOptions<StrategiDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Guide> Guides => Set<Guide>();
    public DbSet<WikiEntry> WikiEntries => Set<WikiEntry>();
    public DbSet<ItemAdvice> Advice => Set<ItemAdvice>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Game>().HasIndex(g => g.Slug).IsUnique();
        b.Entity<Game>().HasMany(g => g.Guides).WithOne(x => x.Game).HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Guide>().HasIndex(x => new { x.GameId, x.Section });
        b.Entity<WikiEntry>().HasIndex(x => x.GameId);
        b.Entity<ItemAdvice>().ToTable("ItemAdvice").HasIndex(x => new { x.GameId, x.Title }).IsUnique();
    }
}
