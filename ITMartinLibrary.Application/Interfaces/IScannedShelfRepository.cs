using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Interfaces;

public interface IScannedShelfRepository
{
    Task SaveShelvesAsync(IList<ScannedShelf> shelves, CancellationToken ct);
    Task<IList<ScannedShelf>> GetAllWithBooksAsync(CancellationToken ct);
    Task ClearAllAsync(CancellationToken ct);
    Task<bool> HasDataAsync(CancellationToken ct);
}
