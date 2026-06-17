using ITMartin.Magic.Domain.Entities;

namespace ITMartin.Magic.Application.Interfaces;

public interface IMagicCardRepository
{
    Task<IEnumerable<MagicCard>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<MagicCard?> GetByScryfallIdAsync(
        string scryfallId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        MagicCard card,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        MagicCard card,
        CancellationToken cancellationToken = default);
}