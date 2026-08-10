using ITMartinPlayer.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinPlayer.Server.Data;

public class PlayerDbContext(DbContextOptions<PlayerDbContext> options) : DbContext(options)
{
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<KaraokeSession> Sessions => Set<KaraokeSession>();
}
