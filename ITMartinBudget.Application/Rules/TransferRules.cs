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
            Category = Category.Savings,
            BudgetGroup = BudgetGroup.InternalTransfer,
            IsRecurring = true
        },

        new()
        {
            Pattern = "til 7633 0008318157",
            Title = "Ratepension",
            Category = Category.Savings,
            BudgetGroup = BudgetGroup.InternalTransfer,
            IsRecurring = true
        },

        new()
        {
            Pattern = "opsparingskonto",
            Title = "Savings Transfer",
            Category = Category.Savings,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "boerneopsparing",
            Title = "Child Savings",
            Category = Category.Savings,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "9490 71557243",
            Title = "Internal Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "7633 8318157",
            Title = "Internal Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "til 7633",
            Title = "Internal Bank Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },new()
        {
            Pattern = "tant og fjas",
            Title = "Private Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },new()
        {
            Pattern = "7264 1259824",
            Title = "Internal Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },new()
        {
            Pattern = "telenor",
            Title = "Phone",
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
            Pattern = "akademikernes a kasse",
            Title = "A-Kasse",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

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
            Pattern = "matas",
            Title = "Health & Beauty",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "salling stormagasin",
            Title = "Shopping",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ruth",
            Title = "Private Payment",
            Category = Category.Other,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "socialpaedagogernes landsforbund",
            Title = "Union",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "hog hinnerup",
            Title = "Sports",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "parkering",
            Title = "Parking",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "parkeringskompagniet",
            Title = "Parking",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "alka",
            Title = "Insurance",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "hm",
            Title = "Clothing",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "about you",
            Title = "Clothing",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kjaer og sommerfeldt",
            Title = "Wine",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "butler loftet",
            Title = "Restaurant",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "zettle",
            Title = "Card Payment",
            Category = Category.Other,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sp alpex",
            Title = "Unknown Subscription",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "salling",
            Title = "Shopping",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "skive tek",
            Title = "Government Payment",
            Category = Category.Other,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "til rip rap og rup",
            Title = "Family Savings",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "parcelforening",
            Title = "Parcelforening",
            Category = Category.Housing,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "hanzo",
            Title = "Restaurant",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "velliv",
            Title = "Pension Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "musikhuset",
            Title = "Culture",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "tsg platforms",
            Title = "Online Entertainment",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "safeticket",
            Title = "Tickets",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sejer",
            Title = "Car / Transport",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },
    ];
}