using ITMartinStats.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinStats.Server.Data;

public class StatsDbContext(DbContextOptions<StatsDbContext> options) : DbContext(options)
{
    public DbSet<PageHit> Hits { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<PageHit>().HasIndex(h => h.CreatedAt);
        b.Entity<PageHit>().HasIndex(h => h.Path);
        b.Entity<PageHit>().HasIndex(h => h.VisitorId);
    }
}
