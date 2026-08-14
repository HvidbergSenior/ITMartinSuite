using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Interfaces;

public interface IBookcaseService
{
    Task<ScanResult> ProcessShelvesAsync(
        Guid groupId,
        IList<(int ShelfNumber, string ImagePath)> shelves,
        IProgress<string>? progress,
        CancellationToken ct);

    Task<IList<ShelfSearchResult>> SearchAsync(Guid groupId, string query, CancellationToken ct);
    Task<int> GetTotalBookCountAsync(Guid groupId, CancellationToken ct);
    Task ClearAllAsync(Guid groupId, CancellationToken ct);
}

public sealed record ScanResult(int NewBooks, int SkippedBooks);

public sealed record ShelfSearchResult
{
    public required int ShelfNumber { get; init; }
    public required string ImagePath { get; init; }
    public required IList<ShelfBook> MatchingBooks { get; init; }
}
