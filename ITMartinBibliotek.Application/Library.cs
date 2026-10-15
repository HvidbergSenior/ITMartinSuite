using ITMartinBibliotek.Domain;

namespace ITMartinBibliotek.Application;

// An album with its songs, ready for the album page.
public sealed record AlbumView(Album Album, IReadOnlyList<PlayerTrack> Tracks);

// A song from search, with what the player needs to start right at it.
public sealed record SongHit(string Name, string Artist, string AlbumName, string PlayId, int Index);

// Browsing and searching the shelf - always only what this viewer owns.
public sealed class Library(IMediaRepository repo, Playback playback, IJellyfin jellyfin)
{
    public Task<List<MediaItem>> ItemsAsync(Viewer viewer) => repo.ForOwnerAsync(viewer.Owner);

    public async Task<List<Album>> AlbumsAsync(Viewer viewer) =>
        Shelf.Albums((await ItemsAsync(viewer)).Where(i => i.Kind == MediaKind.Musik));

    public async Task<List<Artist>> ArtistsAsync(Viewer viewer) => Shelf.Artists(await AlbumsAsync(viewer));

    public async Task<Artist?> ArtistAsync(Viewer viewer, string key) =>
        (await ArtistsAsync(viewer)).FirstOrDefault(a => a.Key == key);

    public async Task<AlbumView?> AlbumAsync(Viewer viewer, string key)
    {
        var album = (await AlbumsAsync(viewer)).FirstOrDefault(a => a.Key == key);
        if (album is null) return null;
        // Jellyfin being away must not hide the album; the page then says the songs could not be fetched.
        List<PlayerTrack> tracks = [];
        try { if (album.CanPlay) tracks = (await playback.AlbumAsync(album.PlayId))?.Tracks ?? []; }
        catch (HttpRequestException) { }
        catch (InvalidOperationException) { }
        catch (TaskCanceledException) { }
        return new AlbumView(album, tracks);
    }

    // Films and series, grouped by the folder they sit in (Film\X-Men\...).
    public async Task<List<MediaItem>> TitlesAsync(Viewer viewer, MediaKind kind) =>
        (await ItemsAsync(viewer)).Where(i => i.Kind == kind)
            .OrderBy(i => Shelf.SortKey(i.Title), StringComparer.Ordinal).ToList();

    public async Task<SearchResult> SearchAsync(Viewer viewer, string query) =>
        Shelf.Search(await ItemsAsync(viewer), query);

    // Songs by name, straight from Jellyfin. The household only: Jellyfin's search runs over every
    // library, so a customer would see the household's songs too.
    public async Task<List<SongHit>> SearchSongsAsync(Viewer viewer, string query, int limit = 12)
    {
        if (!viewer.IsAdmin || Shelf.Fold(query).Length < 3) return [];
        var albums = await AlbumsAsync(viewer);
        var hits = new List<SongHit>();
        foreach (var t in await jellyfin.SearchTracksAsync(query, limit))
        {
            // Start the song inside its album as the shelf shows it (both cds of a 2-cd set).
            var album = albums.FirstOrDefault(a => a.Parts.Any(p => p.JellyfinId == t.AlbumId));
            if (album is null) continue;
            var list = await playback.AlbumAsync(album.PlayId);
            var index = list?.Tracks.FindIndex(x => x.Id == t.Id) ?? -1;
            if (index >= 0) hits.Add(new SongHit(t.Name, t.Artist, album.Title, album.PlayId, index));
        }
        return hits;
    }
}
