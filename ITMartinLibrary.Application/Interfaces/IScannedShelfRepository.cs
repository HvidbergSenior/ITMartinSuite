using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Interfaces;

public interface IScannedShelfRepository
{
    Task AddShelvesAsync(IList<ScannedShelf> shelves, CancellationToken ct);
    Task<HashSet<string>> GetExistingTitlesAsync(CancellationToken ct);
    Task<IList<ScannedShelf>> GetAllWithBooksAsync(CancellationToken ct);
    Task ClearAllAsync(CancellationToken ct);
    Task<int> GetTotalBookCountAsync(CancellationToken ct);
}
