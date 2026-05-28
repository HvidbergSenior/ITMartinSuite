using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TransferRules
{
    public static readonly List<TransactionRule> Items =
    [
        SavingsAndPension(
            "til 7633 8119308",
            "Aldersopsparing",
            Category.Opsparing,
            ComparingType.Exact),

        SavingsAndPension(
            "til 7633 0008318157",
            "Ratepension",
            Category.Pension,
            ComparingType.Exact),

        SavingsAndPension(
            "opsparingskonto",
            "Savings Transfer",
            Category.Opsparing),

        SavingsAndPension(
            "boerneopsparing",
            "Child Savings",
            Category.Opsparing),

        InternalAccountTransfer(
            "9490 71557243",
            "Internal Transfer",
            Category.Overfoersel,
            ComparingType.Exact),

        InternalAccountTransfer(
            "7633 8318157",
            "Internal Transfer",
            Category.Overfoersel,
            ComparingType.Exact),

        InternalAccountTransfer(
            "7264 1259824",
            "Internal Transfer",
            Category.Overfoersel,
            ComparingType.Exact),

        InternalAccountTransfer(
            "3627 11254691",
            "Internal Transfer",
            Category.Overfoersel,
            ComparingType.Exact),

        InternalAccountTransfer(
            "6180 17682091",
            "Internal Transfer",
            Category.Overfoersel,
            ComparingType.Exact)
    ];
}