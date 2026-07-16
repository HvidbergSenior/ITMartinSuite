namespace ITMartinBudget.Domain.Entities;

// One row per ledger (Bogshoppen, Hvidberg, ITMartin, ...) recording the
// scope-mode choice made on /shop-upload - lets the raw CSV importer skip
// scope classification entirely for a single-scope ledger, and lets
// /shop-categorize and /shop-overview hide UI/sections that don't apply.
public class LedgerConfig
{
    public string LedgerId { get; set; } = string.Empty;

    public Enums.LedgerScopeMode ScopeMode { get; set; } = Enums.LedgerScopeMode.Both;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
