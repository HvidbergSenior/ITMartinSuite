using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class PetsRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "zooplus",
            Title = "Zooplus",
            Category = Category.Kaeledyr,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "anicura",
            Title = "AniCura",
            Category = Category.Kaeledyr,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "dyrlaege",
            Title = "Dyrlæge",
            Category = Category.Kaeledyr,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "maxizoo",
            Title = "Maxi Zoo",
            Category = Category.Kaeledyr,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "petworld",
            Title = "Petworld",
            Category = Category.Kaeledyr,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}