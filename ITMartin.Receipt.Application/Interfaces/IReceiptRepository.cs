using ITMartin.Receipt.Domain.Entities;

namespace ITMartin.Receipt.Application.Interfaces;

public interface IReceiptRepository
{
    Task SaveAsync(ReceiptTransaction transaction, CancellationToken cancellationToken = default);
    Task<ReceiptTransaction?> GetReferenceAsync(string merchantName, CancellationToken cancellationToken = default);
}
