using ITMartin.Magic.Application.Models;

public sealed class CardMatchingScoringService
    : ICardMatchScoringService
{
    public decimal CalculateScore(
        ScryfallCard card,
        MagicCardAnalysisResult analysis)
    {
        decimal score = 0;

        // move all current scoring here

        return score;
    }
}