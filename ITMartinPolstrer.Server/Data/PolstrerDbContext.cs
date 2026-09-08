using ITMartinPolstrer.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinPolstrer.Server.Data;

public sealed class PolstrerDbContext(DbContextOptions<PolstrerDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();
}
