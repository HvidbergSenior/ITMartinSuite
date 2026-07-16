using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Domain.Entities;

public class BankTransaction
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    public string Description { get; set; } =
        string.Empty;

    public string NormalizedDescription { get; set; } =
        string.Empty;

    public decimal Amount { get; set; }

    public TransactionType TransactionType { get; set; }

    public Category Category { get; set; }

    public BudgetGroup BudgetGroup { get; set; }

    public string Title { get; set; } =
        string.Empty;


    public DateTime ImportedAt { get; set; } =
        DateTime.UtcNow;

    public decimal RecurringIntervalMonths { get; set; }

    // Separates entirely distinct budgets sharing this one app/database - the
    // original family budget ("family") vs. e.g. a shop's own bank account
    // ("bogshoppen"). Rows from different ledgers are never aggregated
    // together and dedup is scoped per-ledger.
    public string LedgerId { get; set; } = "family";

    // Only meaningful within a ledger where business and private money share
    // one account - see TransactionScope.
    public TransactionScope Scope { get; set; } = TransactionScope.Unknown;

    public BusinessCategory BusinessCategory { get; set; } = BusinessCategory.Unknown;

    // Account balance immediately after this transaction, straight from the
    // bank export - only the raw/positional importer captures this (the
    // family import's header format doesn't include it). Lets the dashboard
    // plot the real balance trend instead of only a computed running sum.
    public decimal? Balance { get; set; }

    // Raw bank exports often carry the real signal (payment processor name,
    // CVR number, invoice text) in extra reference/info columns rather than
    // the short counterparty description - kept so scope classification can
    // see it and so a human reviewing an Unknown transaction has the full
    // context instead of just a cryptic counterparty name. Empty for the
    // family ledger's header-based import, which has no such columns.
    public string RawDetails { get; set; } = string.Empty;

    // Set once a CategoryRule matching this transaction's NormalizedDescription
    // has been assigned (see CategoryRule) - takes precedence over the
    // auto-classified BusinessCategory/Category when present, since it's the
    // user's own final word on what this recurring item actually is.
    public string? UserCategoryName { get; set; }
}