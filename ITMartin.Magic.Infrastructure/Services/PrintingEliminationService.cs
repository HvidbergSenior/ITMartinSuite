using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class PrintingEliminationService
    : IPrintingEliminationService
{
    private readonly MagicDbContext _db;

    public PrintingEliminationService(
        MagicDbContext db)
    {
        _db = db;
    }

    public async Task<List<ScryfallCard>> EliminateAsync(
        IEnumerable<ScryfallCard> cards,
        MagicCardAnalysisResult analysis,
        CancellationToken cancellationToken)
    {
        var sets =
            await _db.Sets.ToDictionaryAsync(
                x => x.SetCode,
                cancellationToken);

        var result =
            cards.ToList();

        if (!string.IsNullOrWhiteSpace(
                analysis.OuterBorder))
        {
            if (analysis.OuterBorder.Contains(
                    "white",
                    StringComparison.OrdinalIgnoreCase))
            {
                result =
                    result
                        .Where(card =>
                            sets.TryGetValue(
                                card.Set,
                                out var set) &&
                            set.UsesWhiteBorder)
                        .ToList();
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.FrameStyle))
        {
            if (analysis.FrameStyle.Contains(
                    "old",
                    StringComparison.OrdinalIgnoreCase))
            {
                result =
                    result
                        .Where(card =>
                            sets.TryGetValue(
                                card.Set,
                                out var set) &&
                            set.UsesOldFrame)
                        .ToList();
            }
        }

        return result;
    }
}