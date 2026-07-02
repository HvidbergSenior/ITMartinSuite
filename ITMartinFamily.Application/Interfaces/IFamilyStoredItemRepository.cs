using ITMartinFamily.Domain.Entities;

namespace ITMartinFamily.Application.Interfaces;

public interface IFamilyStoredItemRepository
{
    Task<List<FamilyStoredItem>> GetRecentAsync(Guid familyId, int count = 20, CancellationToken ct = default);
    Task<List<FamilyStoredItem>> SearchAsync(Guid familyId, string query, CancellationToken ct = default);
    Task<FamilyStoredItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<FamilyStoredItem> SaveAsync(Guid familyId, string name, string location, string? notes, string? photoPath, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
