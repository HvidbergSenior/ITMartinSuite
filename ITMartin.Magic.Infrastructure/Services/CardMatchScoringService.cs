using ITMartin.Magic.Application.Models;

public sealed class CardMatchScoringService
    : ICardMatchScoringService
{
    public decimal CalculateScore(
        ScryfallCard card,
        MagicCardAnalysisResult analysis)
    {
        decimal score = 0;

        if (!string.Equals(
                analysis.IdentifiedName,
                card.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        score += 100;

        if (!string.IsNullOrWhiteSpace(
                analysis.CollectorNumber))
        {
            if (string.Equals(
                    analysis.CollectorNumber,
                    card.CollectorNumber,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;
            }
            else
            {
                score -= 500;
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
            }
        }

        if (!string.IsNullOrWhiteSpace(
                analysis.PowerToughness))
        {
            if ($"{card.Power}/{card.Toughness}" ==
                analysis.PowerToughness)
            {
                score += 100;
            }
        }

        return score;
    }
}