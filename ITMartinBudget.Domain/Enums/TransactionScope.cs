namespace ITMartinBudget.Domain.Enums;

// Only meaningful for ledgers where business and private money share one
// account (e.g. a small shop owner's single bank account) - a family ledger
// has no need to distinguish this and every row stays Unknown.
public enum TransactionScope
{
    Unknown = 0,
    Business = 1,
    Private = 2
}
