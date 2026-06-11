using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class PrintingEliminationService
    : IPrintingEliminationService
{
    private readonly IMagicSetKnowledgeService
        _knowledge;

    public PrintingEliminationService(
        IMagicSetKnowledgeService knowledge)
    {
        _knowledge = knowledge;
    }

    public async Task<List<ScryfallCard>>
        EliminateAsync(
            IEnumerable<ScryfallCard> cards,
            MagicCardAnalysisResult analysis,
            CancellationToken cancellationToken)
    {
        var result =
            cards.ToList();

        Console.WriteLine();
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            "PRINTING ELIMINATION");
        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            $"STARTING PRINTINGS: {result.Count}");

        result =
            EliminateByCollectorNumber(
                result,
                analysis);

        result =
            EliminateByArtist(
                result,
                analysis);

        result =
            await EliminateByFrameAsync(
                result,
                analysis,
                cancellationToken);

        result =
            await EliminateByBorderAsync(
                result,
                analysis,
                cancellationToken);

        result =
            await EliminateBySymbolAsync(
                result,
                analysis,
                cancellationToken);

        result =
            await EliminateBySymbolColorAsync(
                result,
                analysis,
                cancellationToken);

        result =
            await EliminateByCopyrightAsync(
                result,
                analysis,
                cancellationToken);

        Console.WriteLine(
            $"FINAL PRINTINGS: {result.Count}");

        foreach (var card in result)
        {
            Console.WriteLine(
                $" -> {card.Name} [{card.Set}] #{card.CollectorNumber}");
        }

        return result;
    }

    private static List<ScryfallCard>
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

        var matches =
            cards
                .Where(x =>
                    x.CollectorNumber ==
                    analysis.CollectorNumber)
                .ToList();

        Console.WriteLine(
            $"Collector Number: {before} -> {matches.Count}");

        return matches.Count > 0
            ? matches
            : cards;
    }

    private static List<ScryfallCard>
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

        Console.WriteLine(
            $"Artist: {before} -> {matches.Count}");

        return matches.Count > 0
            ? matches
            : cards;
    }

    private async Task<List<ScryfallCard>>
        EliminateByFrameAsync(
            List<ScryfallCard> cards,
            MagicCardAnalysisResult analysis,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                analysis.FrameStyle))
        {
            return cards;
        }

        var before =
            cards.Count;

        var matchingSets =
            await _knowledge
                .SearchByFrameStyleAsync(
                    analysis.FrameStyle,
                    cancellationToken);

        var setCodes =
            matchingSets
                .Select(x => x.SetCode)
                .ToHashSet();

        var result =
            cards
                .Where(x =>
                    setCodes.Contains(x.Set))
                .ToList();

        Console.WriteLine(
            $"Frame Style: {before} -> {result.Count}");

        return result.Count > 0
            ? result
            : cards;
    }

    private async Task<List<ScryfallCard>>
        EliminateByBorderAsync(
            List<ScryfallCard> cards,
            MagicCardAnalysisResult analysis,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                analysis.OuterBorder))
        {
            return cards;
        }

        if (!analysis.OuterBorder.Contains(
                "white",
                StringComparison.OrdinalIgnoreCase))
        {
            return cards;
        }

        var before =
            cards.Count;

        var whiteBorderSets =
            await _knowledge
                .GetWhiteBorderSetsAsync(
                    cancellationToken);

        var result =
            cards
                .Where(x =>
                    whiteBorderSets.Contains(x.Set))
                .ToList();

        Console.WriteLine(
            $"White Border: {before} -> {result.Count}");

        return result.Count > 0
            ? result
            : cards;
    }

    private async Task<List<ScryfallCard>>
        EliminateBySymbolAsync(
            List<ScryfallCard> cards,
            MagicCardAnalysisResult analysis,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                analysis.SetSymbolDescription))
        {
            return cards;
        }

        var before =
            cards.Count;

        var matchingSets =
            await _knowledge
                .SearchBySymbolAsync(
                    analysis.SetSymbolDescription,
                    cancellationToken);

        var setCodes =
            matchingSets
                .Select(x => x.SetCode)
                .ToHashSet();

        var result =
            cards
                .Where(x =>
                    setCodes.Contains(x.Set))
                .ToList();

        Console.WriteLine(
            $"Set Symbol: {before} -> {result.Count}");

        return result.Count > 0
            ? result
            : cards;
    }
    private async Task<List<ScryfallCard>>
        EliminateBySymbolColorAsync(
            List<ScryfallCard> cards,
            MagicCardAnalysisResult analysis,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                analysis.SetSymbolColor))
        {
            return cards;
        }

        var before =
            cards.Count;

        var matchingSets =
            await _knowledge
                .SearchBySymbolColorAsync(
                    analysis.SetSymbolColor,
                    cancellationToken);

        var setCodes =
            matchingSets
                .Select(x => x.SetCode)
                .ToHashSet();

        var result =
            cards
                .Where(x =>
                    setCodes.Contains(x.Set))
                .ToList();

        Console.WriteLine(
            $"Symbol Color: {before} -> {result.Count}");

        return result.Count > 0
            ? result
            : cards;
    }
    private async Task<List<ScryfallCard>>
        EliminateByCopyrightAsync(
            List<ScryfallCard> cards,
            MagicCardAnalysisResult analysis,
            CancellationToken cancellationToken)
    {
        if (!int.TryParse(
                analysis.CopyrightText,
                out var year))
        {
            return cards;
        }

        var before =
            cards.Count;

        var matchingSets =
            await _knowledge
                .SearchByCopyrightYearAsync(
                    year,
                    cancellationToken);

        var setCodes =
            matchingSets
                .Select(x => x.SetCode)
                .ToHashSet();

        var result =
            cards
                .Where(x =>
                    setCodes.Contains(x.Set))
                .ToList();

        Console.WriteLine(
            $"Copyright Year: {before} -> {result.Count}");

        return result.Count > 0
            ? result
            : cards;
    }
}