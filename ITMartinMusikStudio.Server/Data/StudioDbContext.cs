using ITMartinMusikStudio.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMusikStudio.Server.Data;

public sealed class StudioDbContext(DbContextOptions<StudioDbContext> options) : DbContext(options)
{
    public DbSet<StudioSong> Songs => Set<StudioSong>();
}
