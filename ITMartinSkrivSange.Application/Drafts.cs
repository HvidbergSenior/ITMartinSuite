using ITMartinSkrivSange.Domain.Studio;

namespace ITMartinSkrivSange.Application;

// The old one-page wizard (/skriv) on the studio's drafts: songs with no source file and no Spotify link.
public sealed class Drafts(IStudioDraftStore store, IStudioLibrary library, TimeProvider clock)
{
    public async Task<IReadOnlyList<StudioSong>> ListAsync(CancellationToken ct = default) =>
        (await store.ListAsync(ct))
            .Where(s => StepPlanner.GetFlow(s) == SongFlow.Writing)
            .OrderByDescending(s => s.UpdatedAt)
            .ToList();

    public Task<StudioSong?> GetAsync(string key, CancellationToken ct = default) => store.GetAsync(key, ct);

    /// <summary>A new draft under a key made from the title (with a short tail if the key is taken).</summary>
    public async Task<StudioSong> CreateAsync(string title, CancellationToken ct = default)
    {
        var baseKey = library.SafeKey(title);
        var key = await store.KeyExistsAsync(baseKey, ct) ? baseKey + "_" + Guid.NewGuid().ToString("N")[..4] : baseKey;
        var song = new StudioSong { Key = key, Title = title.Trim() };
        await store.AddAsync(song, ct);
        return song;
    }

    public Task SaveAsync(StudioSong song, CancellationToken ct = default)
    {
        song.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        return store.SaveAsync(song, ct);
    }

    public List<SongStep> Steps(StudioSong song) => StepPlanner.GetSteps(song, library.GetSketches(song.Key).Count > 0);

    public List<SketchFile> Sketches(string songKey) => library.GetSketches(songKey);

    public bool DeleteSketch(SketchFile sketch) => library.Delete(sketch.RelativePath);
}
