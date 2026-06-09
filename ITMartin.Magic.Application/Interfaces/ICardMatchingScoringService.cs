using ITMartin.Magic.Application.Models;

public interface ICardMatchScoringService
{
    decimal CalculateScore(
        ScryfallCard card,
        MagicCardAnalysisResult analysis);
}