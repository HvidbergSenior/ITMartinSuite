using Microsoft.EntityFrameworkCore;

namespace ITMartinMailTriage.Server.Data;

public sealed class MailTriageDbContext(DbContextOptions<MailTriageDbContext> options)
    : DbContext(options)
{
    public DbSet<TriagedEmail> Emails => Set<TriagedEmail>();
    public DbSet<TriageProfile> Profile => Set<TriageProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TriagedEmail>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ScoredAtUtc); // fast "still unscored" lookups
            e.HasIndex(x => x.ReceivedAtUtc);
        });
    }
}
