using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class FoodRules
{
    public static readonly List<TransactionRule> Items =
    [
        new()
        {
            Pattern = "foetex",
            Title = "Føtex",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "netto",
            Title = "Netto",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rema",
            Title = "Rema 1000",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "lidl",
            Title = "Lidl",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "loevbjerg",
            Title = "Løvbjerg",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kvickly",
            Title = "Kvickly",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "meny",
            Title = "Meny",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "wolt",
            Title = "Wolt",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "justeat",
            Title = "Just Eat",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "mcdonald",
            Title = "McDonalds",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "burger king",
            Title = "Burger King",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "tgtg",
            Title = "Too Good To Go",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "coop365",
            Title = "Coop 365",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "vdk spar",
            Title = "Spar",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "dagligbrugsen",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "city p hus",
            Title = "Parking",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "bager",
            Title = "Bakery & Cafe",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "marked",
            Title = "Market",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "stof",
            Title = "Creative / Hobby",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "musikcafe",
            Title = "Music & Cafe",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "genbrug",
            Title = "Second Hand",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "dagligbrugsen",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "dk coop",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "365discount",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "dagli brugsen",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "dagligbrugsen",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "norma",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "reenberg groent",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "dagli brugsen",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "reenberg groent",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

    ];
}