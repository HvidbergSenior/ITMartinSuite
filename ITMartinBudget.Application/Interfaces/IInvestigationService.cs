using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Interfaces;

public interface IInvestigationService
{
    // User-triggered only (a button click per cluster) - never called in bulk
    // or automatically, so cost stays negligible even on a large ledger.
    Task<InvestigationResult> InvestigateAsync(
        string label,
        string sampleRawDetails,
        int count,
        decimal totalAmount,
        CancellationToken cancellationToken = default);

    // One call for the whole category list (not one per category) - suggests
    // groups of small, related categories (Shell/Q8/Uno-X) that could be
    // merged into one broader one (Benzin), for /shop-categories. Purely
    // suggestions - nothing is merged until the user reviews and confirms.
    Task<List<MergeSuggestion>> SuggestMergesAsync(
        IReadOnlyList<(string Name, int Count, decimal Sum)> categories,
        CancellationToken cancellationToken = default);
}
