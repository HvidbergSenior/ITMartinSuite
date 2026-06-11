using ITMartin.Magic.Application.Models;

public interface IPrintingEliminationService
{
    Task<List<ScryfallCard>> EliminateAsync(
        IEnumerable<ScryfallCard> cards,
        MagicCardAnalysisResult analysis,
        CancellationToken cancellationToken);
}