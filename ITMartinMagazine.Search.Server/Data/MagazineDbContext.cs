using ITMartinMagazine.Search.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMagazine.Search.Server.Data;

public sealed class MagazineDbContext(DbContextOptions<MagazineDbContext> options) : DbContext(options)
{
    public DbSet<MagazineEntry> Magazines => Set<MagazineEntry>();
}
