using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class VariableIncomeRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "feriepenge",
            Title = "Feriepenge",
            Category = Category.Indkomst,
            BudgetGroup = BudgetGroup.VariableIncome
        },

        new()
        {
            Pattern = "bonus",
            Title = "Bonus",
            Category = Category.Indkomst,
            BudgetGroup = BudgetGroup.VariableIncome
        },

        new()
        {
            Pattern = "overskydende skat",
            Title = "Tax Return",
            Category = Category.Indkomst,
            BudgetGroup = BudgetGroup.VariableIncome
        },

        new()
        {
            Pattern = "foedevarecheck",
            Title = "Government Support",
            Category = Category.Indkomst,
            BudgetGroup = BudgetGroup.VariableIncome
        },new()
        {
            Pattern = "aarhus kommune",
            Title = "Government Income",
            Category = Category.Indkomst,
            BudgetGroup = BudgetGroup.VariableIncome,
            TransactionType = TransactionType.Indkomst
        },
        new()
        {
            Pattern = "rente",
            Title = "Interest",
            Category = Category.Indkomst,
            BudgetGroup = BudgetGroup.VariableIncome,
            TransactionType = TransactionType.Indkomst
        },
    ];
}