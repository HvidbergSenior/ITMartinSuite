using ITMartinSkrivSange.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinSkrivSange.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// ConnectionStrings:SongDb (own, default /app/songdata/skrivsange.db), ConnectionStrings:StudioDb (the studio's,
    /// default /app/data/studio.db) and MusicSettings:Root (default /musik).
    /// </summary>
    public static IServiceCollection AddSkrivSangeInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContextFactory<SongDbContext>(o =>
            o.UseSqlite(config.GetConnectionString("SongDb") ?? "Data Source=/app/songdata/skrivsange.db"));
        services.AddDbContext<StudioDbContext>(o =>
            o.UseSqlite(config.GetConnectionString("StudioDb") ?? "Data Source=/app/data/studio.db"));

        services.AddSingleton<ISongStore, EfSongStore>();
        services.AddScoped<IStudioDraftStore, EfStudioDraftStore>();
        services.AddSingleton<IStudioLibrary>(new StudioLibrary(config["MusicSettings:Root"] ?? "/musik"));
        services.AddSingleton<IStemSeparator, DemucsStemSeparator>();
        services.AddSingleton<IChordDetector, LibrosaChordDetector>();
        return services;
    }

    /// <summary>Creates the own db, and adds the studio.db columns that came after its first schema (safe to re-run).</summary>
    public static void UpgradeSkrivSangeDatabases(this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        using (var songDb = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SongDbContext>>().CreateDbContext())
        {
            if (songDb.Database.GetDbConnection().DataSource is { Length: > 0 } file && Path.GetDirectoryName(Path.GetFullPath(file)) is { } dir)
                Directory.CreateDirectory(dir);
            songDb.Database.EnsureCreated();
        }

        // The studio's db sits on the NAS share. When it can't be reached, the new songs still work - only the old drafts are gone.
        var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
        try { db.Database.EnsureCreated(); }
        catch (Exception) { return; }
        // Shared with the studio over SMB - WAL is unsafe there, see the studio's Program.cs.
        try { db.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE"); } catch { }
        // Only the columns that are missing (asking first keeps "duplicate column" errors out of the log).
        var have = db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('Songs')").ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var column in new[]
                 {
                     "FingerpickPattern TEXT NOT NULL DEFAULT ''", "StrumPattern TEXT NOT NULL DEFAULT ''", "Artist TEXT NOT NULL DEFAULT ''",
                     "SpotifyTrackId TEXT NULL", "SpotifyTrackLabel TEXT NULL", "SyncedLyrics TEXT NULL", "SkippedSteps TEXT NOT NULL DEFAULT ''",
                     "CoverImagePath TEXT NOT NULL DEFAULT ''", "SectionTimings TEXT NULL", "LineBeats TEXT NULL",
                 }.Where(c => !have.Contains(c.Split(' ')[0])))
        {
            try { db.Database.ExecuteSqlRaw("ALTER TABLE Songs ADD COLUMN " + column); } catch { }
        }
    }
}
