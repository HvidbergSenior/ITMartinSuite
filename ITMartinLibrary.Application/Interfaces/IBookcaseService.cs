using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Interfaces;

public interface IBookcaseService
{
    Task ProcessShelvesAsync(
        IList<(int ShelfNumber, string ImagePath)> shelves,
        IProgress<string>? progress,
        CancellationToken ct);

    Task<IList<ShelfSearchResult>> SearchAsync(string query, CancellationToken ct);
    Task<bool> HasDataAsync(CancellationToken ct);
    Task ClearAllAsync(CancellationToken ct);
}

public sealed record ShelfSearchResult
{
    public required int ShelfNumber { get; init; }
    public required string ImagePath { get; init; }
    public required IList<ShelfBook> MatchingBooks { get; init; }
}
