using ITMartinBibliotek.Domain;

namespace ITMartinBibliotek.Application;

// Putting a disc on the list and keeping its details right: scan, look up, edit, remove.
// Every call is one lookup per user action, never a loop over the catalog.
public sealed class Catalog(IMediaRepository repo, IMetadataLookup lookup, IJellyfin jellyfin)
{
    // Null when the item is not this viewer's - a guessed id never opens someone else's disc.
    public async Task<MediaItem?> GetAsync(Viewer viewer, Guid id)
    {
        var item = await repo.GetAsync(id);
        return item is not null && item.Owner == viewer.Owner ? item : null;
    }

    public MediaItem New(Viewer viewer, string barcode) =>
        new() { Barcode = barcode, Format = MediaFormat.Dvd, Owner = viewer.Owner };

    public Task<string> PlayUrlAsync(MediaItem item) =>
        item.JellyfinId == "" ? Task.FromResult("") : jellyfin.PlayUrlAsync(item.JellyfinId);

    public async Task SaveAsync(MediaItem item, bool isNew)
    {
        item.Title = item.Title.Trim();
        item.UpdatedAt = DateTime.UtcNow;
        if (isNew) await repo.AddAsync(item);
        else await repo.UpdateAsync(item);
    }

    public Task DeleteAsync(MediaItem item) => repo.DeleteAsync(item.Id);

    public Task<List<Candidate>> FindAsync(MediaItem item)
    {
        var query = item.Kind == MediaKind.Musik && item.Artist != "" ? $"{item.Artist} {item.Title}" : item.Title;
        return lookup.SearchTitleAsync(query, item.Kind);
    }

    public static void Apply(MediaItem item, Candidate c)
    {
        item.Title = c.Title;
        if (c.Artist != "") item.Artist = c.Artist;
        item.Kind = c.Kind;
        item.Year ??= c.Year;
        if (c.CoverUrl != "") item.CoverUrl = c.CoverUrl;
        if (c.TmdbId != "") item.TmdbId = c.TmdbId;
        if (c.MusicBrainzId != "") item.MusicBrainzId = c.MusicBrainzId;
        if (c.DiscogsId != "") item.DiscogsId = c.DiscogsId;
    }

    // A scanned barcode: the disc if it is already on the list, and what the lookups think it is.
    public async Task<(MediaItem? existing, List<Candidate> candidates)> ScanAsync(Viewer viewer, string code) =>
        (await repo.ByBarcodeAsync(viewer.Owner, code), await lookup.ByBarcodeAsync(code));

    public async Task<MediaItem> AddFromCandidateAsync(Viewer viewer, string barcode, Candidate c)
    {
        var item = new MediaItem
        {
            Title = c.Title, Artist = c.Artist, Year = c.Year, CoverUrl = c.CoverUrl, Kind = c.Kind,
            Format = c.Format ?? (c.Kind == MediaKind.Musik ? MediaFormat.Cd : MediaFormat.Dvd),
            Barcode = barcode, TmdbId = c.TmdbId, MusicBrainzId = c.MusicBrainzId, DiscogsId = c.DiscogsId,
            Owner = viewer.Owner,
        };
        await repo.AddAsync(item);
        return item;
    }

    public Task<List<Candidate>> LookupTitleAsync(string query, MediaKind kind) => lookup.SearchTitleAsync(query, kind);
}

// The household's settings page: Jellyfin and lookup keys, test, sync.
public sealed class Admin(ISettings settings, IJellyfin jellyfin, JellyfinSync sync)
{
    public Task<Dictionary<string, string>> SettingsAsync() => settings.GetAllAsync();
    public Task SetAsync(string key, string value) => settings.SetAsync(key, value);
    public Task<(bool ok, string message)> TestAsync() => jellyfin.TestAsync();
    public Task<SyncResult> SyncAsync() => sync.RunAsync();
    public Task<string> JellyfinHomeAsync() => jellyfin.HomeUrlAsync();
    public Task<string> PlayUrlAsync(string jellyfinId) => jellyfin.PlayUrlAsync(jellyfinId);
}
