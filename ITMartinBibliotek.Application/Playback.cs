using System.Collections.Concurrent;
using ITMartinBibliotek.Domain;

namespace ITMartinBibliotek.Application;

// Bibliotek's own music player. A play id is one Jellyfin album id, or several joined with '_'
// for an album the shelf shows as one (a 2-cd set Jellyfin split in two).
public sealed class Playback(IMediaRepository repo, IJellyfin jellyfin)
{
    // The phone fetches each track in many byte ranges, and every range resolves
    // album + position to a track, so joined track lists are kept for a while.
    private readonly ConcurrentDictionary<string, (DateTime at, PlayerAlbum album)> _joined = new();

    public static IReadOnlyList<string> Parts(string playId) => playId.Split(Shelf.PlayIdSeparator);

    public static bool IsPlayId(string playId) => Parts(playId) is { Count: > 0 and <= 10 } p && p.All(JellyfinIds.IsId);

    // A customer may only play albums from their own library - checked on every request,
    // including each byte range, because a Jellyfin id alone is guessable.
    public async Task<bool> MayPlayAsync(Viewer viewer, string playId)
    {
        if (!IsPlayId(playId)) return false;
        if (viewer.IsAdmin) return true;
        foreach (var id in Parts(playId))
            if (!await repo.OwnsAsync(viewer.Owner, id)) return false;
        return true;
    }

    public async Task<PlayerAlbum?> AlbumAsync(string playId)
    {
        var ids = Parts(playId);
        if (ids.Count == 1) return await jellyfin.AlbumAsync(ids[0]);
        if (_joined.TryGetValue(playId, out var hit) && DateTime.UtcNow - hit.at < TimeSpan.FromMinutes(10))
            return hit.album;

        PlayerAlbum? first = null;
        var tracks = new List<PlayerTrack>();
        for (var disc = 0; disc < ids.Count; disc++)
        {
            var part = await jellyfin.AlbumAsync(ids[disc]);
            if (part is null) continue;
            first ??= part;
            // A part that is only a second copy of songs already listed is skipped, so two copies of
            // the same album do not play every song twice. A real cd 2 is kept whole - a live album
            // may well have the same song twice.
            if (tracks.Count > 0 && part.Tracks.All(t => tracks.Any(x => SameSong(x, t)))) continue;
            tracks.AddRange(part.Tracks.Select(t => t with { Disc = disc + 1 }));
        }
        if (first is null) return null;
        var album = first with { Id = playId, Name = Shelf.SplitDisc(first.Name).Title, Tracks = tracks };
        _joined[playId] = (DateTime.UtcNow, album);
        return album;
    }

    public static bool SameSong(PlayerTrack a, PlayerTrack b) =>
        Shelf.Fold(a.Name) == Shelf.Fold(b.Name) && Math.Abs(a.Seconds - b.Seconds) <= 2;

    public async Task<HttpResponseMessage?> OpenTrackAsync(string playId, int index, string range, CancellationToken ct)
    {
        var album = await AlbumAsync(playId);
        if (album is null || index < 0 || index >= album.Tracks.Count) return null;
        return await jellyfin.OpenAudioAsync(album.Tracks[index].Id, range, ct);
    }

    // Played marks go to the household's Jellyfin account, so a customer's listening stays out of it.
    public Task MarkPlayedAsync(Viewer viewer, string trackId) =>
        viewer.IsAdmin && JellyfinIds.IsId(trackId) ? jellyfin.MarkPlayedAsync(trackId) : Task.CompletedTask;
}
