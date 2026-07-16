namespace ITMartinBudget.Domain.Entities;

// Cached result of the "🔍 Undersøg" AI lookup on /shop-categorize, keyed by
// ledger+pattern - computed once, shown automatically on every later page
// load without re-calling Claude. Purely informational (never sets Scope or
// UserCategoryName itself), so it's safe to keep even after the user picks a
// different answer than the suggestion.
public class TransactionInvestigation
{
    public string LedgerId { get; set; } = string.Empty;

    public string Pattern { get; set; } = string.Empty;

    public string Reasoning { get; set; } = string.Empty;

    public string SuggestedScope { get; set; } = string.Empty;

    public string Confidence { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
