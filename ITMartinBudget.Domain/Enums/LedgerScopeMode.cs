namespace ITMartinBudget.Domain.Enums;

// A one-time choice made on /shop-upload the first time a ledger is created,
// stored in LedgerConfig. Bogshoppen genuinely mixes business and private
// money in one account (Both); Hvidberg (the family's own account) is
// entirely private; ITMartin (the consulting business account) is entirely
// business - so there's nothing ambiguous to classify or ask the user about
// scope-wise for those two.
public enum LedgerScopeMode
{
    Both = 0,
    BusinessOnly = 1,
    PrivateOnly = 2,
}
