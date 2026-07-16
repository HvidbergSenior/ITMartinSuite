using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

// Rule-based, deliberately simple (first match wins) - mirrors
// TransactionCategorizer's approach but kept separate since "business vs
// private" only matters for a mixed-purpose ledger (see BankTransaction.Scope)
// and the family budget should never be touched by this. Anything that
// doesn't match a rule is left Unknown rather than guessed - a wrong "no
// problem here" is worse than an honest "needs a human to look at this",
// and every transaction (including Unknown) is always shown in the UI.
public class TransactionScopeClassifier : ITransactionScopeClassifier
{
    private sealed record Rule(string Marker, TransactionScope Scope, BusinessCategory Category);

    // Ordered - first match wins. Markers are matched against the combined,
    // lowercased Description + Info1 + Info2 + Note text.
    private static readonly List<Rule> Rules = new()
    {
        // ---- Private override, checked first: an explicit "privat" marker
        // on a transaction (e.g. "husleje privat", "til privat") means the
        // owner's own bank text is telling us this specific transfer is the
        // private portion, even when other words in the same line (like
        // "husleje") would otherwise match a business rule further down. ----
        new("priv", TransactionScope.Private, BusinessCategory.PrivateDraw),

        // ---- Business: revenue (card-payment settlements) ----
        new("flatpay", TransactionScope.Business, BusinessCategory.Revenue),
        new("shift4", TransactionScope.Business, BusinessCategory.Revenue),

        // ---- Business: rent (commercial lease via NewSec property management) ----
        new("newsec", TransactionScope.Business, BusinessCategory.Rent),
        new("fællesregnskab", TransactionScope.Business, BusinessCategory.Rent), // "fællesregnskab"
        new("husleje", TransactionScope.Business, BusinessCategory.Rent),

        // ---- Business: generic shop/invoice signals ----
        new("bogshoppen", TransactionScope.Business, BusinessCategory.Other),
        new("cvr-nr", TransactionScope.Business, BusinessCategory.Other),
        new("kundenr", TransactionScope.Business, BusinessCategory.Other),

        // ---- Private: recurring personal bills ----
        new("codan forsikring", TransactionScope.Private, BusinessCategory.PrivateDraw),
        new("betaling fra codan", TransactionScope.Private, BusinessCategory.PrivateDraw), // insurance payout, still personal
        new("a-kasse", TransactionScope.Private, BusinessCategory.PrivateDraw),
        new("telenor", TransactionScope.Private, BusinessCategory.PrivateDraw),
        new("pure gym", TransactionScope.Private, BusinessCategory.PrivateDraw),
        new("modstrøm", TransactionScope.Private, BusinessCategory.PrivateDraw), // "modstrøm" - assumed personal electricity; verify if the shop pays its own utilities separately

        // ---- Private: groceries / everyday shopping ----
        new("rema 1000", TransactionScope.Private, BusinessCategory.PrivateDraw),
        new("netto", TransactionScope.Private, BusinessCategory.PrivateDraw),
        new("føtex", TransactionScope.Private, BusinessCategory.PrivateDraw), // "føtex"
        new("lidl", TransactionScope.Private, BusinessCategory.PrivateDraw),
        new("kvickly", TransactionScope.Private, BusinessCategory.PrivateDraw),
        new("matas", TransactionScope.Private, BusinessCategory.PrivateDraw),
        new("apotek", TransactionScope.Private, BusinessCategory.PrivateDraw),

        // ---- Private: person-to-person MobilePay (the shop's own card
        // payments come through Flatpay/Shift4, not MobilePay) ----
        new("mobilepay", TransactionScope.Private, BusinessCategory.PrivateDraw),
    };

    public void Classify(BankTransaction transaction)
    {
        var text = (
            transaction.Description + " " +
            transaction.RawDetails
        ).ToLowerInvariant();

        var match = Rules.FirstOrDefault(r => text.Contains(r.Marker));
        if (match is null)
        {
            transaction.Scope = TransactionScope.Unknown;
            transaction.BusinessCategory = BusinessCategory.Unknown;
            return;
        }

        transaction.Scope = match.Scope;
        transaction.BusinessCategory = match.Category;
    }
}
