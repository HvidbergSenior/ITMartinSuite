using ITMartinKaraoke.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinKaraoke.Server.Data;

public class KaraokeDbContext(DbContextOptions<KaraokeDbContext> options) : DbContext(options)
{
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<KaraokeSession> Sessions => Set<KaraokeSession>();
}
