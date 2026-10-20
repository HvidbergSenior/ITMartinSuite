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

public interface IBoxClaimRepository
{
    Task<BoxClaim?> GetAsync(string boxCode, CancellationToken ct);
    /// <summary>Adds or updates.</summary>
    Task SaveAsync(BoxClaim claim, CancellationToken ct);
    Task<List<BoxClaim>> AllAsync(CancellationToken ct);
    /// <summary>The helper's boxes not marked done, latest first.</summary>
    Task<List<BoxClaim>> OpenForAsync(int helperId, CancellationToken ct);
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
    Task<int> CountAsync(string boxCode, CancellationToken ct);
    /// <summary>Per box: photos.</summary>
    Task<Dictionary<string, int>> PerBoxAsync(CancellationToken ct);
}

public interface IScanRepository
{
    /// <summary>Stores the scan, or one more copy on the unsent scan of the same code in the same box. Returns the copies.</summary>
    Task<int> AddOrCountAsync(string code, string boxCode, Taker who, CancellationToken ct);
    Task<List<Scan>> UnsentAsync(int max, CancellationToken ct);
    Task MarkSentAsync(IReadOnlyCollection<int> ids, DateTime now, CancellationToken ct);
    Task<int> CountAsync(string boxCode, CancellationToken ct);
    /// <summary>Per box: scanned copies.</summary>
    Task<Dictionary<string, int>> PerBoxAsync(CancellationToken ct);
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
