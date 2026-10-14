using ITMartinSkrivSange.Domain;

namespace ITMartinSkrivSange.Application;

// Writing a song part by part (plan 2026-10-14): six boxes, each with chords in open grips + capo and lyric notes.
// Every change is saved at once - the page has no Gem button.
public sealed class SongWriting(ISongStore store, IOwnProgressionStore own, TimeProvider clock)
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

    /// <summary>A library progression, or one of Martin's own (id "own-...").</summary>
    public async Task UseProgressionAsync(Song song, PartKind kind, string progressionId, CancellationToken ct = default)
    {
        if (OwnProgression.IsOwnId(progressionId))
        {
            var mine = (await own.ListAsync(ct)).FirstOrDefault(p => p.ProgressionId == progressionId) ?? throw new SongException("Akkordrækken findes ikke længere.");
            await Change(song, s => s.UseChords(kind, mine.Chords, mine.ProgressionId, Now), ct);
            return;
        }
        var p = ProgressionLibrary.Find(progressionId) ?? throw new SongException("Ukendt akkordrække.");
        await Change(song, s => s.UseProgression(kind, p, Now), ct);
    }

    public Task<IReadOnlyList<OwnProgression>> OwnProgressionsAsync(CancellationToken ct = default) => own.ListAsync(ct);

    /// <summary>
    /// Chords typed by hand ("Am, G, Am, G") go into the part and are kept as one of Martin's own - "MartinChord n" unless
    /// a name is given. The same chords typed again reuse the saved one.
    /// </summary>
    public async Task<OwnProgression> UseOwnChordsAsync(Song song, PartKind kind, string? typed, string? name, CancellationToken ct = default)
    {
        var chords = ChordInput.Parse(typed);
        var (mine, isNew) = OwnProgression.FindOrCreate(await own.ListAsync(ct), chords, name, Now);
        if (isNew) await own.AddAsync(mine, ct);
        await Change(song, s => s.UseChords(kind, mine.Chords, mine.ProgressionId, Now), ct);
        return mine;
    }

    /// <summary>Removes it from the list. Songs that use it keep their chords.</summary>
    public Task DeleteOwnAsync(OwnProgression progression, CancellationToken ct = default) => own.DeleteAsync(progression.Id, ct);

    public Suggestion Suggest(Song song, PartKind kind) => ProgressionAdvisor.Suggest(song, kind);

    /// <summary>Puts the suggestion in the part and returns it, so the page can show why it fits.</summary>
    public async Task<Suggestion> SuggestAndUseAsync(Song song, PartKind kind, CancellationToken ct = default)
    {
        var suggestion = ProgressionAdvisor.Suggest(song, kind);
        await Change(song, s => s.UseChords(kind, suggestion.Chords, suggestion.ProgressionId, Now), ct);
        return suggestion;
    }

    /// <summary>Alternatives for one chord and the chords that sound nice after it, in the song's key.</summary>
    public (IReadOnlyList<ChordIdea> Instead, IReadOnlyList<ChordIdea> Next) ChordIdeas(Song song, string chord) =>
        (ChordAdvisor.Alternatives(chord, song.HomeGrip), ChordAdvisor.Next(chord, song.HomeGrip));

    public Task ReplaceChordAsync(Song song, PartKind kind, int index, string chord, CancellationToken ct = default) =>
        Change(song, s => s.ReplaceChord(kind, index, chord, Now), ct);

    public Task InsertChordAfterAsync(Song song, PartKind kind, int index, string chord, CancellationToken ct = default) =>
        Change(song, s => s.InsertChordAfter(kind, index, chord, Now), ct);

    public Task RemoveChordAsync(Song song, PartKind kind, int index, CancellationToken ct = default) =>
        Change(song, s => s.RemoveChord(kind, index, Now), ct);

    public Task ClearChordsAsync(Song song, PartKind kind, CancellationToken ct = default) => Change(song, s => s.ClearChords(kind, Now), ct);

    public Task WriteNotesAsync(Song song, PartKind kind, string? notes, CancellationToken ct = default) => Change(song, s => s.WriteNotes(kind, notes, Now), ct);

    public Task DeleteAsync(Song song, CancellationToken ct = default) => store.DeleteAsync(song.Id, ct);

    private async Task Change(Song song, Action<Song> change, CancellationToken ct)
    {
        change(song);
        await store.SaveAsync(song, ct);
    }
}
