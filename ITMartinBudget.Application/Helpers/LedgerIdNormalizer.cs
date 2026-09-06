namespace ITMartinBudget.Application.Helpers;

// LedgerId is matched with plain case-sensitive equality everywhere
// downstream (no COLLATE NOCASE on the column) - found live 2026-09-06 when
// a browser auto-capitalized the Konto-id upload field and "Bogshoppen"
// silently forked into a brand-new, disconnected ledger instead of merging
// into the real "bogshoppen". Every place a ledger id is accepted from user
// input (typed into a field, or a manually-edited URL) must normalize
// through this before it ever reaches a query or a new row.
public static class LedgerIdNormalizer
{
    public static string Normalize(
        string? ledgerId)
    {
        return ledgerId?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
