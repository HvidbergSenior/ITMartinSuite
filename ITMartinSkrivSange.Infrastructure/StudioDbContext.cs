using ITMartinSkrivSange.Application;
using ITMartinSkrivSange.Domain.Studio;
using Microsoft.EntityFrameworkCore;

namespace ITMartinSkrivSange.Infrastructure;

// The studio's studio.db - shared with Musik Studio, which owns the schema.
public sealed class StudioDbContext(DbContextOptions<StudioDbContext> options) : DbContext(options)
{
    public DbSet<StudioSong> Songs => Set<StudioSong>();

    // Without this, EF Core's SQLite provider binds Guid query parameters in
    // a form that doesn't match how EnsureCreated() actually stored the Id
    // column (plain TEXT) - every WHERE Id = @guid silently matches 0 rows
    // (SaveChangesAsync sees 0 rows affected and throws
    // DbUpdateConcurrencyException, so every edit looked like it saved but
    // never actually persisted). Forcing Guid<->string conversion makes
    // parameter binding match the column's real TEXT storage.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StudioSong>()
            .Property(s => s.Id)
            .HasConversion<string>();
    }
}

// Scoped per Blazor circuit: the songs it hands out stay tracked, so SaveAsync just writes what changed.
internal sealed class EfStudioDraftStore(StudioDbContext db) : IStudioDraftStore
{
    public async Task<IReadOnlyList<StudioSong>> ListAsync(CancellationToken ct = default) => await db.Songs.ToListAsync(ct);

    public Task<StudioSong?> GetAsync(string key, CancellationToken ct = default) => db.Songs.FirstOrDefaultAsync(s => s.Key == key, ct);

    public Task<bool> KeyExistsAsync(string key, CancellationToken ct = default) => db.Songs.AnyAsync(s => s.Key == key, ct);

    public async Task AddAsync(StudioSong song, CancellationToken ct = default)
    {
        db.Songs.Add(song);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(StudioSong song, CancellationToken ct = default)
    {
        if (db.Entry(song).State == EntityState.Detached) db.Songs.Update(song);
        await db.SaveChangesAsync(ct);
    }
}
