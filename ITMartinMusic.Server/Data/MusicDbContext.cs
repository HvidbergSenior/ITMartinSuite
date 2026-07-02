using ITMartinMusic.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMusic.Server.Data;

public sealed class MusicDbContext(DbContextOptions<MusicDbContext> options) : DbContext(options)
{
    public DbSet<Song>        Songs        => Set<Song>();
    public DbSet<PracticeEntry> PracticeEntries => Set<PracticeEntry>();
    public DbSet<SongComment> SongComments => Set<SongComment>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Song>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasMany(s => s.PracticeEntries)
             .WithOne(p => p.Song)
             .HasForeignKey(p => p.SongId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PracticeEntry>(e => e.HasKey(p => p.Id));

        model.Entity<SongComment>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.SongKey);
        });
    }
}
