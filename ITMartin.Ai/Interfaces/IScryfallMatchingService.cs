using ITMartin.Ai.Models;

namespace ITMartin.Ai.Interfaces;

public interface IScryfallMatchingService
{
    Task<CardMatchResult> MatchAsync(
        MagicCardAnalysisResult analysis,
        CancellationToken cancellationToken);
}