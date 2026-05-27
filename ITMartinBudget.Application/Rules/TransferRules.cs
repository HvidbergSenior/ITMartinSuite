using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class TransferRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "til 7633 8119308",
            Title = "Aldersopsparing",
            Category = Category.Opsparing,
            BudgetGroup = BudgetGroup.InternalTransfer,
            IsRecurring = true
        },

        new()
        {
            Pattern = "til 7633 0008318157",
            Title = "Ratepension",
            Category = Category.Pension,
            BudgetGroup = BudgetGroup.InternalTransfer,
            IsRecurring = true
        },

        new()
        {
            Pattern = "opsparingskonto",
            Title = "Savings Transfer",
            Category = Category.Opsparing,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "boerneopsparing",
            Title = "Child Savings",
            Category = Category.Opsparing,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "9490 71557243",
            Title = "Internal Transfer",
            Category = Category.Overfoersel,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "7633 8318157",
            Title = "Internal Transfer",
            Category = Category.Overfoersel,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "til 7633",
            Title = "Internal Bank Transfer",
            Category = Category.Overfoersel,
            BudgetGroup = BudgetGroup.InternalTransfer
        },new()
        {
            Pattern = "7264 1259824",
            Title = "Internal Transfer",
            Category = Category.Overfoersel,
            BudgetGroup = BudgetGroup.InternalTransfer
        },
        new()
        {
            Pattern = "3627 11254691",
            Title = "Internal Transfer",
            Category = Category.Overfoersel,
            BudgetGroup = BudgetGroup.InternalTransfer
        },
        new()
        {
            Pattern = "6180 17682091",
            Title = "Internal Transfer",
            Category = Category.Overfoersel,
            BudgetGroup = BudgetGroup.InternalTransfer
        },
    ];
}