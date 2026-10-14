using ITMartinSkrivSange.Domain.Studio;

namespace ITMartinSkrivSange.Application;

/// <param name="Key">The SafeKey stems, meta and takes are filed under.</param>
public sealed record VaultSong(string RelativePath, string Key, SongMeta Meta, StemResult? Stems, List<RecordingFile> Recordings);

// Song Vault: pick a recording from the music folder, split it into vocals/instrumental, find chords and tempo, sing on top.
public sealed class Vault(IStudioLibrary library, IStemSeparator stems, IChordDetector chords)
{
    public bool CanSplit => stems.IsAvailable;
    public bool CanDetect => chords.IsAvailable;

    public List<SourceFile> Sources() => library.GetSourceFiles();

    public VaultSong Open(string relativePath)
    {
        var key = library.SafeKey(relativePath);
        return new(relativePath, key, library.LoadMeta(key), library.GetExistingStems(key), library.GetRecordings(key));
    }

    public List<RecordingFile> Recordings(VaultSong song) => library.GetRecordings(song.Key);

    public async Task<StemResult?> SplitAsync(VaultSong song, CancellationToken ct = default)
    {
        await stems.SeparateAsync(FullPath(song), library.StemsDir(song.Key), ct);
        return library.GetExistingStems(song.Key);
    }

    /// <returns>The chord chart as "mm:ss chord" lines.</returns>
    public async Task<string> DetectChordsAsync(VaultSong song, CancellationToken ct = default) =>
        ChordChart.Format(await chords.DetectAsync(FullPath(song), ct));

    public Task<double?> DetectTempoAsync(VaultSong song, CancellationToken ct = default) => chords.DetectTempoAsync(FullPath(song), ct);

    public void SaveMeta(VaultSong song, SongMeta meta) => library.SaveMeta(song.Key, meta);

    /// <summary>The stem file as a path under the music folder, for the player.</summary>
    public string StemPath(VaultSong song, string file) => $"stems/{song.Key}/{file}";

    private string FullPath(VaultSong song) =>
        library.Resolve(song.RelativePath) ?? throw new FileNotFoundException("Filen findes ikke længere.", song.RelativePath);
}
