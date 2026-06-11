using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface IPrintingEliminationService
{
    Task<List<ScryfallCard>> EliminateAsync(
        IEnumerable<ScryfallCard> cards,
        MagicCardAnalysisResult analysis,
        CancellationToken cancellationToken);
}