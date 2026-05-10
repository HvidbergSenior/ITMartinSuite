using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Infrastructure.Seeding;

public static class CategoryRuleSeeder
{
    public static List<CategoryRule> Get()
    {
        return
        [
            // =====================================
            // FIXED INCOME
            // =====================================

            new()
            {
                Pattern = "loen",
                Category = Category.FixedIncome,
                MatchType = MatchingType.Contains,
                Priority = 100
            },

            new()
            {
                Pattern = "loenoverfoersel",
                Category = Category.FixedIncome,
                MatchType = MatchingType.Contains,
                Priority = 100
            },

            new()
            {
                Pattern = "maanedsloen",
                Category = Category.FixedIncome,
                MatchType = MatchingType.Contains,
                Priority = 100
            },

            new()
            {
                Pattern = "su",
                Category = Category.FixedIncome,
                MatchType = MatchingType.ExactWord,
                Priority = 100
            },

            // =====================================
            // FOOD
            // =====================================

            new()
            {
                Pattern = "foetex",
                Category = Category.Food,
                MatchType = MatchingType.Contains,
                Priority = 90
            },

            new()
            {
                Pattern = "netto",
                Category = Category.Food,
                MatchType = MatchingType.Contains,
                Priority = 90
            },

            new()
            {
                Pattern = "rema",
                Category = Category.Food,
                MatchType = MatchingType.Contains,
                Priority = 90
            },

            new()
            {
                Pattern = "lidl",
                Category = Category.Food,
                MatchType = MatchingType.Contains,
                Priority = 90
            },

            new()
            {
                Pattern = "wolt",
                Category = Category.Food,
                MatchType = MatchingType.Contains,
                Priority = 90
            },

            // =====================================
            // TRANSPORT
            // =====================================

            new()
            {
                Pattern = "ok",
                Category = Category.Transport,
                MatchType = MatchingType.ExactWord,
                Priority = 90
            },

            new()
            {
                Pattern = "q8",
                Category = Category.Transport,
                MatchType = MatchingType.ExactWord,
                Priority = 90
            },

            new()
            {
                Pattern = "circle k",
                Category = Category.Transport,
                MatchType = MatchingType.Contains,
                Priority = 90
            },

            new()
            {
                Pattern = "easypark",
                Category = Category.Transport,
                MatchType = MatchingType.Contains,
                Priority = 90
            },

            // =====================================
            // FIXED EXPENSES
            // =====================================

            new()
            {
                Pattern = "spotify",
                Category = Category.FixedExpenses,
                MatchType = MatchingType.Contains,
                Priority = 95
            },

            new()
            {
                Pattern = "netflix",
                Category = Category.FixedExpenses,
                MatchType = MatchingType.Contains,
                Priority = 95
            },

            new()
            {
                Pattern = "telenor",
                Category = Category.FixedExpenses,
                MatchType = MatchingType.Contains,
                Priority = 95
            },

            new()
            {
                Pattern = "nrgi",
                Category = Category.FixedExpenses,
                MatchType = MatchingType.Contains,
                Priority = 95
            },

            new()
            {
                Pattern = "jetbrains",
                Category = Category.FixedExpenses,
                MatchType = MatchingType.Contains,
                Priority = 95
            },

            new()
            {
                Pattern = "one com",
                Category = Category.FixedExpenses,
                MatchType = MatchingType.Contains,
                Priority = 95
            },

            // =====================================
            // TRANSFER
            // =====================================

            new()
            {
                Pattern = "mobilepay",
                Category = Category.Transfer,
                MatchType = MatchingType.Contains,
                Priority = 50
            },

            new()
            {
                Pattern = "overfoersel",
                Category = Category.Transfer,
                MatchType = MatchingType.Contains,
                Priority = 50
            },

            new()
            {
                Pattern = "opsparingskonto",
                Category = Category.Savings,
                MatchType = MatchingType.Contains,
                Priority = 70
            }
        ];
    }
}