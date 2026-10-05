using ITMartinBibliotek.Server.Data;
using ITMartinBibliotek.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBibliotek.Server.Services;

public sealed record SyncResult(int InLibrary, int Matched, int Imported, int NoLongerFound);

// Reconciles the shelf catalog with what Jellyfin actually has on disk.
// A catalog item that matches a Jellyfin item flips to "Rippet"; a Jellyfin
// item nobody has catalogued is imported as a digital-only entry so the
// collection page shows the whole library, not just scanned discs.
// Each Jellyfin item belongs to the owner of the library it sits in (Customers),
// and only matches catalog items of that same owner.
public sealed class JellyfinSync(IDbContextFactory<BibliotekDbContext> dbFactory, JellyfinClient jellyfin, SettingsStore settings, Customers customers, ILogger<JellyfinSync> log)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<SyncResult> RunAsync()
    {
        await _gate.WaitAsync();
        try { return await RunLockedAsync(); }
        finally { _gate.Release(); }
    }

    private async Task<SyncResult> RunLockedAsync()
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
            var owner = customers.OwnerForLibrary(jf.Library);
            var item = items.FirstOrDefault(i => i.Owner == owner && i.JellyfinId == jf.Id)
                    ?? items.FirstOrDefault(i => i.Owner == owner && Matches(i, jf));

            if (item is null)
            {
                item = new MediaItem
                {
                    Title = jf.Name,
                    Artist = jf.Artist,
                    Kind = jf.Kind,
                    Format = MediaFormat.Digital,
                    Year = jf.Year,
                    Collection = jf.Collection,
                    TmdbId = jf.TmdbId,
                    MusicBrainzId = jf.MusicBrainzId,
                    Owner = owner,
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
            item.Collection = jf.Collection;
            if (string.IsNullOrEmpty(item.CoverUrl)) item.CoverUrl = await jellyfin.CoverUrlAsync(jf.Id);
            item.UpdatedAt = DateTime.UtcNow;
        }

        // A rip that disappeared from Jellyfin is still a disc on the shelf. The sync also runs
        // unattended now, so a library that suddenly lost most of its items (an NFS mount gone
        // missing) is treated as an outage, not as thousands of deleted rips.
        var goneItems = items.Where(i => i.JellyfinId != "" && !seen.Contains(i.JellyfinId)).ToList();
        var linked = items.Count(i => i.JellyfinId != "");
        if (goneItems.Count > 20 && goneItems.Count * 2 > linked)
        {
            log.LogWarning("Sync: {Gone} of {Linked} linked items missing from Jellyfin - skipping removals", goneItems.Count, linked);
            goneItems.Clear();
        }
        foreach (var i in goneItems)
        {
            i.JellyfinId = "";
            i.Status = MediaStatus.Ejet;
            if (i.Format == MediaFormat.Digital) db.Items.Remove(i);
        }

        await db.SaveChangesAsync();
        await settings.SetAsync(SettingsStore.LastSync, DateTime.UtcNow.ToString("O"));
        return new SyncResult(library.Count, matched, imported, goneItems.Count);
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

// Customers upload CDs through the upload box and expect them in the app without asking
// anyone to press Synkroniser: every 20 minutes their libraries are rescanned and synced.
public sealed class CustomerSyncService(Customers customers, JellyfinClient jellyfin, JellyfinSync sync, ILogger<CustomerSyncService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (customers.All.Count == 0) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(20));
        do
        {
            try
            {
                // Jellyfin scans in the background, so new albums land in the sync after this one.
                foreach (var c in customers.All) await jellyfin.RefreshLibraryAsync(c.Bibliotek);
                await sync.RunAsync();
            }
            catch (Exception ex) { log.LogWarning(ex, "Customer sync failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
