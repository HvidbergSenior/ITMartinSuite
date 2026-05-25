using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class FixedExpenseRules
{
    public static readonly List<TransactionRule> Items =
    [
        // =====================================
        // HOUSING
        // =====================================

        new()
        {
            Pattern = "termin jyske realkredit",
            Title = "Mortgage",
            Category = Category.Housing,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "aarhus kommune ejendomsskat",
            Title = "Property Tax",
            Category = Category.Housing,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "parcelforeningen",
            Title = "Parcelforening",
            Category = Category.Housing,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        // =====================================
        // UTILITIES
        // =====================================

        new()
        {
            Pattern = "aarhus vand",
            Title = "Water",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "kredsloeb",
            Title = "Heating",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "allente",
            Title = "TV",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "telenor",
            Title = "Phone",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        // =====================================
        // INSURANCE
        // =====================================

        new()
        {
            Pattern = "alka forsikring",
            Title = "Insurance",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "sygeforsikringen danmark",
            Title = "Health Insurance",
            Category = Category.Health,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "depotsikring",
            Title = "Insurance",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        // =====================================
        // UNION / MEMBERSHIP
        // =====================================

        new()
        {
            Pattern = "akademikernes a kasse",
            Title = "A-Kasse",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "socialpaedagogernes landsforbund",
            Title = "Union",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        // =====================================
        // SPORTS / FAMILY
        // =====================================

        new()
        {
            Pattern = "hog hinnerup",
            Title = "Sports",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },new()
        {
        Pattern = "skattestyrelsen",
        Title = "Taxes",
        Category = Category.Bills,
        BudgetGroup = BudgetGroup.FixedExpense,
        IsRecurring = true
        },

        new()
        {
            Pattern = "ticketmaster",
            Title = "Tickets",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "dk ok",
            Title = "Fuel",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "vdk q8",
            Title = "Fuel",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "universal music",
            Title = "Music",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "dmr",
            Title = "Vehicle Tax",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "google one",
            Title = "Google One",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },
    ];
}