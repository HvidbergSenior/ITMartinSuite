using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Interfaces;

public interface IScannedShelfRepository
{
    Task AddShelvesAsync(IList<ScannedShelf> shelves, CancellationToken ct);
    Task<HashSet<string>> GetExistingTitlesAsync(Guid groupId, CancellationToken ct);
    Task<IList<ScannedShelf>> GetAllWithBooksAsync(Guid groupId, CancellationToken ct);
    Task ClearAllAsync(Guid groupId, CancellationToken ct);
    Task<int> GetTotalBookCountAsync(Guid groupId, CancellationToken ct);
}
