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
                    x.SymbolKeywords.ToLower()
                        .Contains(word)))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MagicSetKnowledge>>
        SearchByFrameStyleAsync(
            string frameStyle,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                frameStyle))
        {
            return [];
        }

        var normalized =
            frameStyle
                .Trim()
                .ToLowerInvariant();

        return await _db.Sets
            .Where(x =>
                x.FrameStyle.ToLower() ==
                normalized)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MagicSetKnowledge>>
        SearchBySymbolColorAsync(
            string symbolColor,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                symbolColor))
        {
            return [];
        }

        var normalized =
            symbolColor
                .Trim()
                .ToLowerInvariant();

        return await _db.Sets
            .Where(x =>
                x.SymbolColor.ToLower() ==
                normalized)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MagicSetKnowledge>>
        SearchByCopyrightYearAsync(
            int copyrightYear,
            CancellationToken cancellationToken)
    {
        return await _db.Sets
            .Where(x =>
                x.CopyrightYear ==
                copyrightYear)
            .ToListAsync(cancellationToken);
    }
}