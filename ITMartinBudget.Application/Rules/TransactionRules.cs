using ITMartinBudget.Application.Models;
using ITMartinBudget.Application.Rules;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application;

public static class TransactionRules
{
    public static readonly List<TransactionRule> Rules =
    [
        ..IncomeRules.Items,

        ..TransferRules.Items,

        ..FixedExpenseRules.Items,

        ..FoodRules.Items,

        ..TransportRules.Items,

        ..ShoppingRules.Items,

        ..HealthRules.Items,

        ..EntertainmentRules.Items,

        // =====================================
        // FALLBACK MOBILEPAY
        // =====================================

        new()
        {
            Pattern = "mobilepay",
            Title = "MobilePay Expense",
            Category = Category.Other,
            BudgetGroup = BudgetGroup.VariableExpense,
            TransactionType = TransactionType.Udgift
        },

        new()
        {
            Pattern = "mobilepay",
            Title = "MobilePay Income",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.VariableIncome,
            TransactionType = TransactionType.Indkomst
        }
    ];
}