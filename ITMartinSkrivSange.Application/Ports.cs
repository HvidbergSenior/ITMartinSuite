using ITMartinSkrivSange.Domain;
using ITMartinSkrivSange.Domain.Studio;

namespace ITMartinSkrivSange.Application;

// The songs written part by part - SkrivSange's own db (skrivsange.db), not the studio's.
public interface ISongStore
{
    Task<IReadOnlyList<Song>> ListAsync(CancellationToken ct = default);
    Task<Song?> GetAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Song song, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

// Martin's own progressions ("MartinChord 1" ...), shared by all songs - in skrivsange.db too.
public interface IOwnProgressionStore
{
    Task<IReadOnlyList<OwnProgression>> ListAsync(CancellationToken ct = default);
    Task AddAsync(OwnProgression progression, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

// The old drafts in the studio's studio.db (shared with Musik Studio - a draft here shows up there under "Trin i gang").
public interface IStudioDraftStore
{
    Task<IReadOnlyList<StudioSong>> ListAsync(CancellationToken ct = default);
    Task<StudioSong?> GetAsync(string key, CancellationToken ct = default);
    Task<bool> KeyExistsAsync(string key, CancellationToken ct = default);
    Task AddAsync(StudioSong song, CancellationToken ct = default);
    /// <summary>Writes the changes made to a song from this store.</summary>
    Task SaveAsync(StudioSong song, CancellationToken ct = default);
}

// Sidecar metadata for Song Vault - one JSON file per song, keyed by the file's SafeKey like stems/recordings.
public sealed record SongMeta(string Lyrics = "", string Chords = "", double? Tempo = null);
public sealed record SourceFile(string RelativePath, string Title);
public sealed record RecordingFile(string RelativePath, string Name, DateTime CreatedAt, bool IsVideo, bool IsAi = false, string? Section = null);
public sealed record SketchFile(string RelativePath, string Name, DateTime CreatedAt);
public sealed record StemResult(string? Vocals, string? Drums, string? Bass, string? Other, string? Instrumental = null);

// The music folder (/musik): source files, takes, sketches, stems, meta.
public interface IStudioLibrary
{
    string SafeKey(string name);
    /// <summary>The full path for a path under the music folder, or null when it points outside it or doesn't exist.</summary>
    string? Resolve(string relativePath);
    List<SourceFile> GetSourceFiles();
    SongMeta LoadMeta(string songKey);
    void SaveMeta(string songKey, SongMeta meta);
    StemResult? GetExistingStems(string songKey);
    string StemsDir(string songKey);
    List<RecordingFile> GetRecordings(string songKey);
    Task<string> SaveRecordingAsync(string songKey, Stream content, string? contentType, string? section);
    List<SketchFile> GetSketches(string songKey);
    Task<string> SaveSketchAsync(string songKey, Stream content, string? contentType);
    bool Delete(string relativePath);
}

// Demucs (Python) - splits a song into vocals and instrumental. Not installed in the light container.
public interface IStemSeparator
{
    bool IsAvailable { get; }
    Task<StemResult> SeparateAsync(string inputPath, string stemsOutputDir, CancellationToken ct = default);
}

// librosa (Python) - listens for chords and tempo. Not installed in the light container.
public interface IChordDetector
{
    bool IsAvailable { get; }
    Task<List<ChordSegment>> DetectAsync(string inputPath, CancellationToken ct = default);
    Task<double?> DetectTempoAsync(string inputPath, CancellationToken ct = default);
}
