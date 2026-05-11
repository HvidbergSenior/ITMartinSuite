using ITMartin.Media.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
namespace ITMartin.Media.Infrastructure;

public sealed class MediaDbContext
    : DbContext
{
    public MediaDbContext(
        DbContextOptions<MediaDbContext> options)
        : base(options)
    {
    }

    public DbSet<AiCache> AiCache => Set<AiCache>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiCache>()
            .HasKey(x => x.Hash);

        base.OnModelCreating(modelBuilder);
    }
}