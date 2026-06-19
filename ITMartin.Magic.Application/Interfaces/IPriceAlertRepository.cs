using ITMartin.Magic.Domain.Entities;

namespace ITMartin.Magic.Application.Interfaces;

public interface IPriceAlertRepository
{
    Task<List<PriceAlert>> GetActiveAsync(CancellationToken ct = default);
    Task AddAsync(PriceAlert alert, CancellationToken ct = default);
    Task DismissAsync(Guid id, CancellationToken ct = default);
}
