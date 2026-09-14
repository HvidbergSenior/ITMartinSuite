using ITMartinBibliotek.Server.Data;
using ITMartinBibliotek.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBibliotek.Server.Services;

public sealed record SyncResult(int InLibrary, int Matched, int Imported, int NoLongerFound);

// Reconciles the shelf catalog with what Jellyfin actually has on disk.
// A catalog item that matches a Jellyfin item flips to "Rippet"; a Jellyfin
// item nobody has catalogued is imported as a digital-only entry so the
// collection page shows the whole library, not just scanned discs.
public sealed class JellyfinSync(IDbContextFactory<BibliotekDbContext> dbFactory, JellyfinClient jellyfin, SettingsStore settings)
{
    public async Task<SyncResult> RunAsync()
    {
        var library = await jellyfin.ListLibraryAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var items = await db.Items.ToListAsync();

        var matched = 0;
        var imported = 0;
        var seen = new HashSet<string>();

        foreach (var jf in library)
        {
            seen.Add(jf.Id);
            var item = items.FirstOrDefault(i => i.JellyfinId == jf.Id)
                    ?? items.FirstOrDefault(i => Matches(i, jf));

            if (item is null)
            {
                item = new MediaItem
                {
                    Title = jf.Name,
                    Artist = jf.Artist,
                    Kind = jf.Kind,
                    Format = MediaFormat.Digital,
                    Year = jf.Year,
                    TmdbId = jf.TmdbId,
                    MusicBrainzId = jf.MusicBrainzId,
                };
                db.Items.Add(item);
                items.Add(item);
                imported++;
            }
            else
            {
                matched++;
            }

            item.JellyfinId = jf.Id;
            item.Status = MediaStatus.Rippet;
            if (string.IsNullOrEmpty(item.TmdbId)) item.TmdbId = jf.TmdbId;
            if (string.IsNullOrEmpty(item.MusicBrainzId)) item.MusicBrainzId = jf.MusicBrainzId;
            item.Year ??= jf.Year;
            if (string.IsNullOrEmpty(item.CoverUrl)) item.CoverUrl = await jellyfin.CoverUrlAsync(jf.Id);
            item.UpdatedAt = DateTime.UtcNow;
        }

        // A rip that disappeared from Jellyfin is still a disc on the shelf.
        var gone = 0;
        foreach (var i in items.Where(i => i.JellyfinId != "" && !seen.Contains(i.JellyfinId)))
        {
            i.JellyfinId = "";
            i.Status = MediaStatus.Ejet;
            if (i.Format == MediaFormat.Digital) db.Items.Remove(i);
            gone++;
        }

        await db.SaveChangesAsync();
        await settings.SetAsync(SettingsStore.LastSync, DateTime.UtcNow.ToString("O"));
        return new SyncResult(library.Count, matched, imported, gone);
    }

    private static bool Matches(MediaItem i, JellyfinItem jf)
    {
        if (i.JellyfinId != "") return false;
        if (i.TmdbId != "" && jf.TmdbId != "") return i.TmdbId == jf.TmdbId;
        if (i.MusicBrainzId != "" && jf.MusicBrainzId != "") return i.MusicBrainzId == jf.MusicBrainzId;
        if (i.Kind != jf.Kind) return false;
        return TitleMatch.Same(i.Title, i.Year, jf.Name, jf.Year);
    }
}
