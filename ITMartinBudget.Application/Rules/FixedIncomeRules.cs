using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class FixedIncomeRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "loenoverfoersel",
            Title = "Salary",
            Category = Category.Indkomst,
            BudgetGroup = BudgetGroup.FixedIncome,
            IsRecurring = true
        },

        new()
        {
            Pattern = "maanedsloen",
            Title = "Salary",
            Category = Category.Indkomst,
            BudgetGroup = BudgetGroup.FixedIncome,
            IsRecurring = true
        },

        new()
        {
            Pattern = "plusloen",
            Title = "Salary",
            Category = Category.Indkomst,
            BudgetGroup = BudgetGroup.FixedIncome,
            IsRecurring = true
        },

        new()
        {
            Pattern = "su",
            Title = "SU",
            Category = Category.Indkomst,
            BudgetGroup = BudgetGroup.FixedIncome,
            IsRecurring = true
        }
    ];
}