using ITMartin.Receipt.Domain.Entities;

namespace ITMartin.Receipt.Application.Interfaces;

public interface IReceiptRepository
{
    Task SaveAsync(ReceiptTransaction transaction, CancellationToken cancellationToken = default);
    Task<List<ReceiptTransaction>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ReceiptTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetTemplateAsync(Guid id, bool isTemplate, CancellationToken cancellationToken = default);
    Task<ReceiptTransaction?> GetTemplateAsync(string merchantName, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
