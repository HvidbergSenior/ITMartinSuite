using ITMartinTjek.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartinTjek.Infrastructure;

public sealed class TjekDbContext(DbContextOptions<TjekDbContext> options) : DbContext(options)
{
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
}
