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

    /// <summary>Card names that start like the text (Scryfall autocomplete, free) - "find the edition by name" (2026-11-13).</summary>
    Task<List<string>> AutocompleteAsync(string text, CancellationToken cancellationToken);

    /// <summary>The set codes a card (exact name) was printed in, newest first.</summary>
    Task<List<string>> PrintingSetCodesAsync(string cardName, CancellationToken cancellationToken);

    Task<(decimal? Eur, decimal? Usd)?> GetPriceByIdAsync(
        string scryfallId,
        CancellationToken cancellationToken);
}