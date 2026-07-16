using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Interfaces;

public sealed record TransactionCluster(
    string Pattern,
    string Label,
    int Count,
    decimal Sum,
    TransactionScope Scope,
    string? CurrentCategoryName,
    DateTime FirstDate,
    DateTime LastDate,
    string? SampleRawDetails,
    // Cached "🔍 Undersøg" result (see TransactionInvestigation) - present
    // once investigated, shown automatically without re-calling Claude.
    string? InvestigationReasoning,
    string? InvestigationSuggestedScope,
    string? InvestigationConfidence);

// IsMixedScope is true when a category (usually after a manual merge) spans
// more than one Scope - the name/badge shown to the user should say so
// rather than silently picking one scope to display.
// IsPeopleCategory is true when every underlying pattern behind this
// category is a MobilePay-to-a-named-person pattern (see /shop-categorize's
// "one category per person" convention) - lets /shop-categories group the
// (often numerous) person categories separately from everything else.
public sealed record LedgerCategorySummary(string Name, int Count, decimal Sum, TransactionScope Scope, bool IsMixedScope, bool IsPeopleCategory);

public interface ICategoryRuleService
{
    // Every distinct NormalizedDescription group in the ledger (the smallest
    // natural clustering unit - "Føtex", "Husleje" - not a broad category).
    // Sorted so the clusters most needing a human decision come first: still
    // Unknown scope, then Business, then already-Private (usually the least
    // urgent since it's not part of the shop's own P&L) - largest total
    // first within each group.
    Task<List<TransactionCluster>> GetClustersAsync(string ledgerId, CancellationToken cancellationToken = default);

    // Distinct category names already in use for this ledger, for the
    // "existing category" half of the assignment dropdown.
    Task<List<string>> GetExistingCategoryNamesAsync(string ledgerId, CancellationToken cancellationToken = default);

    // Saves the rule and immediately applies it to every currently-matching
    // transaction in the ledger, not just future imports.
    Task AssignAsync(string ledgerId, string pattern, string categoryName, TransactionScope scope, CancellationToken cancellationToken = default);

    // One row per distinct category name actually in use (not per pattern) -
    // for /shop-categories, where the user merges several small categories
    // (Shell, Q8, Uno-X) into one broader one (Benzin) after the initial
    // per-pattern categorization pass is done.
    Task<List<LedgerCategorySummary>> GetCategorySummaryAsync(string ledgerId, CancellationToken cancellationToken = default);

    // Renames every CategoryRule and every transaction's UserCategoryName
    // from any of sourceNames to targetName - the underlying per-pattern
    // rules stay intact (still one rule per pattern), only the category
    // label they point at changes, so future imports keep matching correctly.
    Task MergeCategoriesAsync(string ledgerId, List<string> sourceNames, string targetName, CancellationToken cancellationToken = default);

    // Flips every transaction and CategoryRule with this exact category name
    // to newScope - lets /shop-categories fix a whole category's scope in
    // one click (e.g. a private-looking category that turns out to be real
    // business spending) instead of re-doing it pattern by pattern.
    Task SetCategoryScopeAsync(string ledgerId, string categoryName, TransactionScope newScope, CancellationToken cancellationToken = default);
}
