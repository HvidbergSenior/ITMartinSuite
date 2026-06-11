using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Domain.Entities;
using ITMartin.Magic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class MagicSetKnowledgeService
    : IMagicSetKnowledgeService
{
    private readonly MagicDbContext _db;

    public MagicSetKnowledgeService(
        MagicDbContext db)
    {
        _db = db;
    }

    public async Task<List<string>>
        GetOldFrameSetsAsync(
            CancellationToken cancellationToken)
    {
        return await _db.Sets
            .Where(x => x.UsesOldFrame)
            .Select(x => x.SetCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<string>>
        GetWhiteBorderSetsAsync(
            CancellationToken cancellationToken)
    {
        return await _db.Sets
            .Where(x => x.UsesWhiteBorder)
            .Select(x => x.SetCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MagicSetKnowledge>>
        SearchBySymbolAsync(
            string symbolDescription,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                symbolDescription))
        {
            return [];
        }

        var words =
            symbolDescription
                .ToLowerInvariant()
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

        return await _db.Sets
            .Where(x =>
                words.Any(word =>
                    x.SymbolKeywords.Contains(word)))
            .ToListAsync(cancellationToken);
    }
}