using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;

public sealed class CardMatchScoringService
    : ICardMatchScoringService
{
    public decimal CalculateScore(
        ScryfallCard card,
        MagicCardAnalysisResult analysis)
    {
        decimal score = 0;

        Console.WriteLine();
        Console.WriteLine(
            $"SCORING [{card.Name}] [{card.Set}]");

        if (!string.Equals(
                analysis.IdentifiedName,
                card.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "FAILED NAME MATCH");

            return 0;
        }

        score += 100;

        Console.WriteLine(
            "+100 Name");

        if (!string.IsNullOrWhiteSpace(
                analysis.CollectorNumber))
        {
            if (string.Equals(
                    analysis.CollectorNumber,
                    card.CollectorNumber,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;

                Console.WriteLine(
                    "+1000 Collector Number");
            }
            else
            {
                score -= 500;

                Console.WriteLine(
                    "-500 Collector Number Mismatch");
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.Artist))
        {
            if (string.Equals(
                    analysis.Artist,
                    card.Artist,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 300;

                Console.WriteLine(
                    "+300 Artist");
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.ManaCost))
        {
            if (string.Equals(
                    analysis.ManaCost,
                    card.ManaCost,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 100;

                Console.WriteLine(
                    "+100 Mana Cost");
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.CardType))
        {
            if (card.TypeLine.Contains(
                    analysis.CardType,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 100;

                Console.WriteLine(
                    "+100 Type");
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.PowerToughness))
        {
            if ($"{card.Power}/{card.Toughness}" ==
                analysis.PowerToughness)
            {
                score += 100;

                Console.WriteLine(
                    "+100 Power/Toughness");
            }
        }

        Console.WriteLine(
            $"FINAL SCORE: {score}");

        return score;
    }
}