using ITMartinSkrivSange.Domain;

namespace ITMartinSkrivSange.Application;

// Writing a song part by part (plan 2026-10-14): six boxes, each with chords in open grips + capo and lyric notes.
// Every change is saved at once - the page has no Gem button.
public sealed class SongWriting(ISongStore store, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<Song>> ListAsync(CancellationToken ct = default) =>
        (await store.ListAsync(ct)).OrderByDescending(s => s.UpdatedAt).ToList();

    public Task<Song?> GetAsync(Guid id, CancellationToken ct = default) => store.GetAsync(id, ct);

    public async Task<Song> CreateAsync(string title, CancellationToken ct = default)
    {
        var song = Song.Create(title, Now);
        await store.SaveAsync(song, ct);
        return song;
    }

    public Task RenameAsync(Song song, string title, CancellationToken ct = default) => Change(song, s => s.Rename(title, Now), ct);
    public Task SetMoodAsync(Song song, string mood, CancellationToken ct = default) => Change(song, s => s.SetMood(mood, Now), ct);
    public Task SetCapoAsync(Song song, int capo, CancellationToken ct = default) => Change(song, s => s.SetCapo(capo, Now), ct);
    public Task SetBpmAsync(Song song, int bpm, CancellationToken ct = default) => Change(song, s => s.SetBpm(bpm, Now), ct);
    public Task SetStrumAsync(Song song, string strumId, CancellationToken ct = default) => Change(song, s => s.SetStrum(strumId, Now), ct);

    public Task UseProgressionAsync(Song song, PartKind kind, string progressionId, CancellationToken ct = default)
    {
        var p = ProgressionLibrary.Find(progressionId) ?? throw new SongException("Ukendt akkordrække.");
        return Change(song, s => s.UseProgression(kind, p, Now), ct);
    }

    public Suggestion Suggest(Song song, PartKind kind) => ProgressionAdvisor.Suggest(song, kind);

    /// <summary>Puts the suggestion in the part and returns it, so the page can show why it fits.</summary>
    public async Task<Suggestion> SuggestAndUseAsync(Song song, PartKind kind, CancellationToken ct = default)
    {
        var suggestion = ProgressionAdvisor.Suggest(song, kind);
        await Change(song, s => s.UseChords(kind, suggestion.Chords, suggestion.ProgressionId, Now), ct);
        return suggestion;
    }

    public Task ClearChordsAsync(Song song, PartKind kind, CancellationToken ct = default) => Change(song, s => s.ClearChords(kind, Now), ct);

    public Task WriteNotesAsync(Song song, PartKind kind, string? notes, CancellationToken ct = default) => Change(song, s => s.WriteNotes(kind, notes, Now), ct);

    public Task DeleteAsync(Song song, CancellationToken ct = default) => store.DeleteAsync(song.Id, ct);

    private async Task Change(Song song, Action<Song> change, CancellationToken ct)
    {
        change(song);
        await store.SaveAsync(song, ct);
    }
}
