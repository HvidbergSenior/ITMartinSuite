using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application;

public static class TransactionRules
{
    public static readonly List<TransactionRule> Rules =
    [

        // =====================================
        // FIXED INCOME
        // =====================================

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
            Pattern = "loen",
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

        // =====================================
        // VARIABLE INCOME
        // =====================================

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

        // =====================================
        // MONEY GIFTS / PRIVATE TRANSFERS
        // =====================================

        new()
        {
            Pattern = "mobilepay bent moeller",
            Title = "Money Gift",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.GiftIncome
        },

        new()
        {
            Pattern = "mobilepay marianne",
            Title = "Private Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.GiftIncome
        },

        // =====================================
        // INTERNAL TRANSFERS
        // =====================================

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
            Pattern = "mobilepay eigil hvidberg",
            Title = "Family Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "mobilepay bertil hvidberg",
            Title = "Family Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "mobilepay julius hvidberg",
            Title = "Family Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        // =====================================
        // FIXED EXPENSES
        // =====================================

        new()
        {
            Pattern = "spotify",
            Title = "Spotify",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "netflix",
            Title = "Netflix",
            Category = Category.Entertainment,
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

        new()
        {
            Pattern = "telenor",
            Title = "Telenor",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "nrgi",
            Title = "NRGi",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "alka",
            Title = "Alka Forsikring",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "aarhus vand",
            Title = "Aarhus Vand",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "allente",
            Title = "Allente",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "kredsloeb",
            Title = "Kredsløb",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "skattestyrelsen motor opkraevning",
            Title = "Motor Registration",
            Category = Category.Bills,
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
            Pattern = "socialpaedagogernes landsforbund",
            Title = "Union",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "jyske realkredit",
            Title = "Jyske Realkredit",
            Category = Category.Housing,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "totalkredit",
            Title = "Totalkredit",
            Category = Category.Housing,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "nykredit",
            Title = "Nykredit",
            Category = Category.Housing,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "realkredit",
            Title = "Realkredit",
            Category = Category.Housing,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "boliglaan",
            Title = "Boliglån",
            Category = Category.Housing,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "huslaan",
            Title = "Huslån",
            Category = Category.Housing,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        // =====================================
        // FOOD
        // =====================================

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
            Pattern = "bilka",
            Title = "Bilka",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        // =====================================
        // TRANSPORT
        // =====================================

        new()
        {
            Pattern = "circle k",
            Title = "Circle K",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "uno x",
            Title = "Uno-X",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "q8",
            Title = "Q8",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ok",
            Title = "OK Benzin",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        // =====================================
        // ENTERTAINMENT
        // =====================================

        new()
        {
            Pattern = "steamgames",
            Title = "Steam",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "playstation",
            Title = "PlayStation",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "google play",
            Title = "Google Play",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        // =====================================
        // FOOD DELIVERY
        // =====================================

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
        },

        // =====================================
        // PARKING / TRAVEL
        // =====================================

        new()
        {
            Pattern = "easypark",
            Title = "EasyPark",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "apcoa",
            Title = "Apcoa",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rejsekort",
            Title = "Rejsekort",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "brobizz",
            Title = "BroBizz",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        // =====================================
        // DEFAULT MOBILEPAY
        // =====================================

        new()
        {
            Pattern = "mobilepay",
            Title = "MobilePay",
            Category = Category.Other,
            BudgetGroup = BudgetGroup.VariableExpense
        }
    ];
}