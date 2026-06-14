using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class PrintingEliminationService
    : IPrintingEliminationService
{

    public PrintingEliminationService()
    {
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
                    string.Equals(
                        x.CollectorNumber,
                        analysis.CollectorNumber,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        Console.WriteLine(
            $"Collector Number: {before} -> {matches.Count}");

        if (matches.Count == 0)
        {
            Console.WriteLine(
                $"Collector Number: {before} -> 0 (ignored)");

            return cards;
        }

        Console.WriteLine(
            $"Collector Number: {before} -> {matches.Count}");

        return matches;
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

   
}