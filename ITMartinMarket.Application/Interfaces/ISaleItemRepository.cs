using ITMartinMarket.Domain.Entities;

namespace ITMartinMarket.Application.Interfaces;

public interface ISaleItemRepository
{
    Task<List<SaleItem>> GetActiveAsync(CancellationToken ct = default);
    Task<List<SaleItem>> GetAllAsync(CancellationToken ct = default);
    Task<SaleItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(SaleItem item, CancellationToken ct = default);
    Task UpdateAsync(SaleItem item, CancellationToken ct = default);
    Task AddBidAsync(Bid bid, CancellationToken ct = default);
    Task AddMessageAsync(ItemMessage message, CancellationToken ct = default);
}
