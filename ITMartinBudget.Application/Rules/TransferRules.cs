using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TransferRules
{
    public static readonly List<TransactionRule> Items =
    [
        VibzSavingsPension(
            "til 7633 8119308",
            "Aldersopsparing",
            Category.Opsparing,
            ComparingType.Exact),

        VibzSavingsPension(
            "til 7633 0008318157",
            "Ratepension",
            Category.Pension,
            ComparingType.Exact),

        SavingsAndPension(
            "opsparingskonto",
            "Til Opsparingskonto",
            Category.Opsparing,
            ComparingType.Contains),

        ChildrenSavings(
            "boerneopsparing",
            "Til Børneopsparing",
            Category.Opsparing,
            ComparingType.Contains),

        Stocks(
            "9490 71557243",
            "NordNet",
            Category.Aktier,
            ComparingType.Exact),

        VibzSavingsPension(
            "7633 8318157",
            "VibzFastOverførsel",
            Category.Opsparing,
            ComparingType.Exact),
    ];
}