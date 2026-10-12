using ITMartinPolstrer.Domain;

namespace ITMartinPolstrer.Application;

// The pieces, their steps, photos and comments (SQLite in Infrastructure). Reads return detached copies with
// steps, media and comments included; writes are small and explicit, each in its own short unit of work.
public interface IPieceStore
{
    Task<Piece?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<List<Piece>> AllAsync(CancellationToken ct = default);
    Task<List<string>> WorkersAsync(CancellationToken ct = default);

    /// <summary>Loads the piece (with steps and media) for change, or creates it with <paramref name="create"/> when the id is
    /// missing or unknown; runs <paramref name="change"/> and saves everything in one go. Returns the saved piece.</summary>
    Task<Piece> SaveGraphAsync(Guid? id, Func<Piece> create, Func<Piece, Task> change, CancellationToken ct = default);

    Task UpdatePieceAsync(Guid id, Action<Piece> change, CancellationToken ct = default);
    Task DeletePieceAsync(Guid id, CancellationToken ct = default);
    Task AddStepAsync(Step step, CancellationToken ct = default);
    Task UpdateStepNoteAsync(Guid stepId, string note, CancellationToken ct = default);
    Task DeleteStepAsync(Guid stepId, CancellationToken ct = default);
    Task DeleteMediaAsync(Guid mediaId, CancellationToken ct = default);
    Task AddCommentAsync(Comment comment, CancellationToken ct = default);
    Task DeleteCommentAsync(Guid commentId, CancellationToken ct = default);
}

// Photos and videos on the data volume, one folder per piece (the gallery app mounts the same tree).
public interface IMediaStore
{
    string NewFolderFor(string title);
    Task<MediaFile> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default);
    /// <summary>Moves an upload buffered to a temp file into place; the temp file is removed either way.</summary>
    Task<MediaFile> SaveFromTempAsync(string folder, string fileName, string tempPath, CancellationToken ct = default);
    void Delete(MediaFile media);
    void DeleteFolder(string folder);
    /// <summary>A draft's "Møbel" folder gets the piece's title; rewrites the media paths (the caller saves them).</summary>
    string RenameFolderForTitle(string oldFolder, string title, IEnumerable<MediaFile> media);
    string FullPath(string relativePath);
}

public enum SuggestionLevel { Hurtig, Grundig }

// One batched Claude call over a piece's photos + notes. SUGGESTIONS only - her own text always wins.
public sealed record Suggestion(
    string NavnType, string Type, string PeriodeStil, string Materialer, string Konstruktion,
    string Stand, string[] Teknikker, string[] SaadanGoerDu, string Usikkerhed)
{
    public string Model { get; init; } = "";
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }

    // Rough price in øre so the user sees what a call cost (Haiku 1/5 $, Opus 5/25 $ per MTok, ~7 kr/$).
    public double Oere => Model.Contains("haiku")
        ? (InputTokens * 1.0 + OutputTokens * 5.0) / 1_000_000 * 700
        : (InputTokens * 5.0 + OutputTokens * 25.0) / 1_000_000 * 700;

    public bool IsQuick => Model.Contains("haiku");
}

public interface IAiSuggester
{
    bool Enabled { get; }
    Task<Suggestion> SuggestAsync(Piece piece, SuggestionLevel level, CancellationToken ct = default);
}
