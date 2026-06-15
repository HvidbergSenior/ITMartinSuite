using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface IScryfallService
{
    Task<CardSearchResult?> SearchAsync(
        string? cardName,
        string? setCode,
        MagicCardAnalysisResult? analysis,
        CancellationToken cancellationToken);
}