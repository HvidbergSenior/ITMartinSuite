using ITMartinDreamReader.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinDreamReader.Server.Data;

public class DreamDbContext(DbContextOptions<DreamDbContext> options) : DbContext(options)
{
    public DbSet<DreamCategory> Categories => Set<DreamCategory>();
    public DbSet<DreamEntry> Entries => Set<DreamEntry>();
}
