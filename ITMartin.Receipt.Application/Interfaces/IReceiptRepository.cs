using ITMartin.Receipt.Domain.Entities;

namespace ITMartin.Receipt.Application.Interfaces;

public interface IReceiptRepository
{
    Task SaveAsync(ReceiptTransaction transaction, CancellationToken cancellationToken = default);
    Task<List<ReceiptTransaction>> GetAllAsync(CancellationToken cancellationToken = default);
}
