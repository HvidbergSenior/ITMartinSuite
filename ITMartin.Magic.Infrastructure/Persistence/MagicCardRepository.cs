using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Magic.Infrastructure.Persistence;

public sealed class MagicCardRepository
    : IMagicCardRepository
{
    private readonly MagicDbContext _db;

    public MagicCardRepository(MagicDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<MagicCard>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Cards
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<MagicCard?> GetByScryfallIdAsync(
        string scryfallId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Cards
            .FirstOrDefaultAsync(
                x => x.ScryfallId == scryfallId,
                cancellationToken);
    }

    public async Task AddAsync(
        MagicCard card,
        CancellationToken cancellationToken = default)
    {
        _db.Cards.Add(card);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        MagicCard card,
        CancellationToken cancellationToken = default)
    {
        _db.Cards.Update(card);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertScannedAsync(
        MagicCard card,
        CancellationToken cancellationToken = default)
    {
        var existing = card.ScryfallId is not null
            ? await GetByScryfallIdAsync(card.ScryfallId, cancellationToken)
            : null;

        if (existing is null)
        {
            _db.Cards.Add(card);
        }
        else
        {
            existing.Quantity++;
            existing.EurPrice = card.EurPrice;
            existing.UsdPrice = card.UsdPrice;
            existing.LastSeenAt = DateTime.UtcNow;
            _db.Cards.Update(existing);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
