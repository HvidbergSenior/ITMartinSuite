using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class HealthRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "tandlaege",
            Title = "Dentist",
            Category = Category.Health,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "apotek",
            Title = "Pharmacy",
            Category = Category.Health,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "fitness",
            Title = "Fitness",
            Category = Category.Health,
            BudgetGroup = BudgetGroup.VariableExpense
        }, new()
        {
            Pattern = "apot",
            Title = "Pharmacy",
            Category = Category.Health,
            BudgetGroup = BudgetGroup.VariableExpense
        },
    ];
}