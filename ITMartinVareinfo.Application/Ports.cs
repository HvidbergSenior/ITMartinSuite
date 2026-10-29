using ITMartinVareinfo.Domain;

namespace ITMartinVareinfo.Application;

// What the use cases need from the outside world. Implemented in ITMartinVareinfo.Infrastructure.

/// <summary>Photo files in the data folder. Paths are relative to it.</summary>
public interface IPhotoStore
{
    /// <summary>Keeps an upload as it is. Throws when it is not a picture at all (only the header is read - fast).</summary>
    Task SaveRawAsync(string path, Stream photo, CancellationToken ct);
    /// <summary>Phone photos (3-12 MB) -> a JPEG of at most ~1568 px, upright.</summary>
    Task<byte[]> ShrinkAsync(Stream photo, CancellationToken ct);
    /// <summary>Close-ups for reading small text: the full-resolution photo cut in 2x2 overlapping parts, each at most
    /// ~1568 px. Empty when the photo is too small for close-ups to show more than the whole picture.</summary>
    Task<List<byte[]>> TilesAsync(Stream photo, CancellationToken ct);
    Task SaveAsync(string path, byte[] jpeg, CancellationToken ct);
    Task<byte[]?> ReadAsync(string path, CancellationToken ct);
    void Delete(string path);
    /// <summary>The full file name when the path is inside the data folder and exists, else null.</summary>
    string? FullPath(string path);
}

public interface IHelperRepository
{
    Task<List<Helper>> ActiveAsync(CancellationToken ct);
    Task<Helper?> ByCodeAsync(string code, CancellationToken ct);
    Task<Helper?> GetAsync(int id, CancellationToken ct);
    Task AddAsync(Helper helper, CancellationToken ct);
    Task UpdateAsync(Helper helper, CancellationToken ct);
}

public interface IPhotoRepository
{
    Task<Photo> AddAsync(Photo photo, CancellationToken ct);
    Task<Photo?> GetAsync(int id, CancellationToken ct);
    Task UpdateAsync(Photo photo, CancellationToken ct);
    /// <summary>Uploads not made ready yet, oldest first.</summary>
    Task<List<Photo>> UnprocessedAsync(int max, CancellationToken ct);
    /// <summary>Ready photos Lager does not have yet, oldest first.</summary>
    Task<List<Photo>> UnsentAsync(int max, CancellationToken ct);
    Task MarkSentAsync(IReadOnlyCollection<int> ids, DateTime now, CancellationToken ct);
    Task<int> CountAsync(int sessionId, CancellationToken ct);
    /// <summary>Per session and helper: photos and the latest.</summary>
    Task<List<HelperTally>> PerSessionAsync(CancellationToken ct);
}

public interface IScanRepository
{
    /// <summary>Stores the scan, or one more copy on the unsent scan of the same code in the same session. Returns the copies.</summary>
    Task<int> AddOrCountAsync(string code, WorkSession session, Taker who, CancellationToken ct);
    Task<List<Scan>> UnsentAsync(int max, CancellationToken ct);
    Task MarkSentAsync(IReadOnlyCollection<int> ids, DateTime now, CancellationToken ct);
    Task<int> CountAsync(int sessionId, CancellationToken ct);
    /// <summary>Per session and helper: scanned copies and the latest.</summary>
    Task<List<HelperTally>> PerSessionAsync(CancellationToken ct);
}

/// <summary>Settings the use cases read (Vareinfo__* in magic.env).</summary>
public sealed record VareinfoOptions
{
    /// <summary>Martin's PIN (Vareinfo__Pin): signs in as "Martin" and opens the Hjælpere page.</summary>
    public string AdminPin { get; init; } = "";
    public string AdminName { get; init; } = "Martin";
    /// <summary>The link to Lager a person clicks (Vareinfo__LagerPublicUrl).</summary>
    public string LagerPublicUrl { get; init; } = "https://lager.bogshoppen.dk";
}

/// <summary>A message for the person, in Danish - shown as it is.</summary>
public sealed class VareinfoException(string message) : Exception(message);

/// <summary>The photo's session is not there (any more) for this person - fx deleted while the photo waited on the phone
/// (2026-10-29: Vibeke's first photos were thrown away like that). The photo itself is fine: the phone keeps it until a
/// session is chosen, so the upload answers 409, not 400.</summary>
public sealed class SessionGoneException() : Exception("Sessionen findes ikke længere - vælg en session, så sendes billedet.");
