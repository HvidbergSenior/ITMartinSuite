using ITMartinMusikStudio.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMusikStudio.Server.Data;

public sealed class StudioDbContext(DbContextOptions<StudioDbContext> options) : DbContext(options)
{
    public DbSet<StudioSong> Songs => Set<StudioSong>();

    // Without this, EF Core's SQLite provider binds Guid query parameters in
    // a form that doesn't match how EnsureCreated() actually stored the Id
    // column (plain TEXT) - every WHERE Id = @guid silently matches 0 rows
    // (SaveChangesAsync sees 0 rows affected and throws
    // DbUpdateConcurrencyException, which every call site here catches and
    // swallows, so every edit - lyrics, notes, chords, anything - looked
    // like it saved but never actually persisted). Forcing Guid<->string
    // conversion makes parameter binding match the column's real TEXT
    // storage. Confirmed via a raw `SELECT COUNT(*) WHERE Id = {0}` with the
    // loaded entity's own Id returning 0 before this fix.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StudioSong>()
            .Property(s => s.Id)
            .HasConversion<string>();
    }
}
