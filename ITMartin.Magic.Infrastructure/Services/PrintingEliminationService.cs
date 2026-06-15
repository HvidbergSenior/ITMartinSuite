using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class PrintingEliminationService
    : IPrintingEliminationService
{
    private readonly ILogger<PrintingEliminationService> _logger;

    public PrintingEliminationService(ILogger<PrintingEliminationService> logger)
    {
        _logger = logger;
    }

    public async Task<List<ScryfallCard>>
        EliminateAsync(
            IEnumerable<ScryfallCard> cards,
            MagicCardAnalysisResult analysis,
            CancellationToken cancellationToken)
    {
        var result =
            cards.ToList();

        _logger.LogDebug("Printing elimination — starting with {Count} printings", result.Count);

        result =
            EliminateByCollectorNumber(
                result,
                analysis);

        result =
            EliminateByArtist(
                result,
                analysis);

        _logger.LogDebug("Printing elimination — {Count} printings remain", result.Count);

        foreach (var card in result)
        {
            _logger.LogDebug("  -> {Name} [{Set}] #{Collector}", card.Name, card.Set, card.CollectorNumber);
        }

        return result;
    }

    private List<ScryfallCard>
        EliminateByCollectorNumber(
            List<ScryfallCard> cards,
            MagicCardAnalysisResult analysis)
    {
        if (string.IsNullOrWhiteSpace(
                analysis.CollectorNumber))
        {
            return cards;
        }

        var before =
            cards.Count;

        var artist =
            NormalizeArtist(
                analysis.Artist);

        var matches =
            cards
                .Where(x =>
                    string.Equals(
                        NormalizeArtist(x.Artist),
                        artist,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (matches.Count == 0)
        {
            _logger.LogDebug("Collector number filter: {Before} -> 0 (no match, ignored)", before);

            return cards;
        }

        _logger.LogDebug("Collector number filter: {Before} -> {After}", before, matches.Count);

        return matches;
    }

    private List<ScryfallCard>
        EliminateByArtist(
            List<ScryfallCard> cards,
            MagicCardAnalysisResult analysis)
    {
        if (string.IsNullOrWhiteSpace(
                analysis.Artist))
        {
            return cards;
        }

        var before =
            cards.Count;

        var matches =
            cards
                .Where(x =>
                    string.Equals(
                        x.Artist,
                        analysis.Artist,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        _logger.LogDebug("Artist filter: {Before} -> {After}", before, matches.Count);

        return matches.Count > 0
            ? matches
            : cards;
    }

    private static string NormalizeArtist(
        string value)
    {
        return value
            .Replace("Illus.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Illustrated by", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}