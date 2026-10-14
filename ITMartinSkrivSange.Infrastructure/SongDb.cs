using System.Text.Json;
using ITMartinSkrivSange.Application;
using ITMartinSkrivSange.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartinSkrivSange.Infrastructure;

// SkrivSange's own db (skrivsange.db, plan 2026-10-14: not the studio's). One row per song, the six parts as JSON.
public sealed class SongDbContext(DbContextOptions<SongDbContext> options) : DbContext(options)
{
    public DbSet<SongRow> Songs => Set<SongRow>();
    public DbSet<OwnProgressionRow> OwnProgressions => Set<OwnProgressionRow>();

    /// <summary>For a db made before the table existed (EnsureCreated only creates a db that is missing).</summary>
    internal const string CreateOwnProgressions =
        "CREATE TABLE IF NOT EXISTS OwnProgressions (Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, Chords TEXT NOT NULL, CreatedAt TEXT NOT NULL)";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SongRow>(e =>
        {
            e.ToTable("Songs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion<string>();
        });
        modelBuilder.Entity<OwnProgressionRow>(e =>
        {
            e.ToTable("OwnProgressions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion<string>();
        });
    }
}

// Chords: space separated, as the grips are written - "Am G Am G".
public sealed class OwnProgressionRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Chords { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

internal sealed class EfOwnProgressionStore(IDbContextFactory<SongDbContext> factory) : IOwnProgressionStore
{
    public async Task<IReadOnlyList<OwnProgression>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await db.OwnProgressions.AsNoTracking().ToListAsync(ct))
            .OrderBy(r => r.CreatedAt)
            .Select(r => new OwnProgression(r.Id, r.Name, r.Chords.Split(' ', StringSplitOptions.RemoveEmptyEntries), DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)))
            .ToList();
    }

    public async Task AddAsync(OwnProgression p, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.OwnProgressions.Add(new OwnProgressionRow { Id = p.Id, Name = p.Name, Chords = string.Join(' ', p.Chords), CreatedAt = p.CreatedAt });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.OwnProgressions.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }
}

public sealed class SongRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Mood { get; set; } = "";
    public int Capo { get; set; }
    public int Bpm { get; set; }
    public string StrumId { get; set; } = "";
    public string PartsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class EfSongStore(IDbContextFactory<SongDbContext> factory) : ISongStore
{
    private sealed record PartDto(string Kind, List<string> Chords, string? ProgressionId, string Notes);

    public async Task<IReadOnlyList<Song>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await db.Songs.AsNoTracking().ToListAsync(ct)).Select(ToSong).ToList();
    }

    public async Task<Song?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Songs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToSong(row);
    }

    public async Task SaveAsync(Song song, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Songs.FirstOrDefaultAsync(x => x.Id == song.Id, ct);
        if (row is null) db.Songs.Add(row = new SongRow { Id = song.Id, CreatedAt = song.CreatedAt });
        row.Title = song.Title;
        row.Mood = song.Mood;
        row.Capo = song.Capo;
        row.Bpm = song.Bpm;
        row.StrumId = song.StrumId;
        row.UpdatedAt = song.UpdatedAt;
        row.PartsJson = JsonSerializer.Serialize(song.Parts.Select(p => new PartDto(p.Kind.ToString(), p.Chords.ToList(), p.ProgressionId, p.Notes)));
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Songs.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
    }

    private static Song ToSong(SongRow r)
    {
        var parts = (JsonSerializer.Deserialize<List<PartDto>>(r.PartsJson) ?? [])
            .Where(p => Enum.TryParse<PartKind>(p.Kind, out _))
            .Select(p => new SongPart(Enum.Parse<PartKind>(p.Kind), p.Chords, p.ProgressionId, p.Notes ?? ""));
        return Song.Restore(r.Id, r.Title, r.Mood, r.Capo, r.Bpm, r.StrumId, parts,
            DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc), DateTime.SpecifyKind(r.UpdatedAt, DateTimeKind.Utc));
    }
}
