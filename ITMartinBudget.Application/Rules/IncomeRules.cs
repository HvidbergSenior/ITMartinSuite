using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class IncomeRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "loenoverfoersel",
            Title = "Salary",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.FixedIncome,
            IsRecurring = true
        },

        new()
        {
            Pattern = "maanedsloen",
            Title = "Salary",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.FixedIncome,
            IsRecurring = true
        },

        new()
        {
            Pattern = "plusloen",
            Title = "Salary",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.FixedIncome,
            IsRecurring = true
        },

        new()
        {
            Pattern = "su",
            Title = "SU",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.FixedIncome,
            IsRecurring = true
        },

        new()
        {
            Pattern = "feriepenge",
            Title = "Feriepenge",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.VariableIncome
        },

        new()
        {
            Pattern = "bonus",
            Title = "Bonus",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.VariableIncome
        },

        new()
        {
            Pattern = "overskydende skat",
            Title = "Tax Return",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.VariableIncome
        },

        new()
        {
            Pattern = "foedevarecheck",
            Title = "Government Support",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.VariableIncome
        },new()
        {
            Pattern = "aarhus kommune",
            Title = "Government Income",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.VariableIncome,
            TransactionType = TransactionType.Indkomst
        },
    ];
}