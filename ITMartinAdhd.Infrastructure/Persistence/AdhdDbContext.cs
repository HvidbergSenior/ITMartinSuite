using ITMartinAdhd.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinAdhd.Infrastructure.Persistence;

public sealed class AdhdDbContext : DbContext
{
    public AdhdDbContext(DbContextOptions<AdhdDbContext> options)
        : base(options) { }

    public DbSet<StoredItem> StoredItems => Set<StoredItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdhdDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
