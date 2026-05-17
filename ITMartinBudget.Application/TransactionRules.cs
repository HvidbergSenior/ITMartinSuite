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

        new()
        {
            Pattern = "foedevarecheck",
            Title = "Government Support",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.VariableIncome
        },

        new()
        {
            Pattern = "danmark",
            Title = "Insurance Reimbursement",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.VariableIncome,
            TransactionType = TransactionType.Indkomst
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

        new()
        {
            Pattern = "mob pay julius hvidberg",
            Title = "Family Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "mob pay bent moller",
            Title = "Family Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "mob pay christian birkefe",
            Title = "Family Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "vdk mob pay dorthe moller joh",
            Title = "Family Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "vdk mob pay bertil hvidberg j",
            Title = "Family Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "vdk mob pay eigil hvidberg jo",
            Title = "Family Transfer",
            Category = Category.Transfer,
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
            Pattern = "til 7633",
            Title = "Internal Bank Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "overfoersel",
            Title = "Transfer",
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
            TransactionType = TransactionType.Udgift,
            IsRecurring = true
        },

        new()
        {
            Pattern = "alka",
            Title = "Insurance Refund",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.VariableIncome,
            TransactionType = TransactionType.Indkomst
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
            Pattern = "3f kontingent",
            Title = "Union",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "fagligt faelles forbund",
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
            Pattern = "ejendomsskat",
            Title = "Property Tax",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense
        },

        new()
        {
            Pattern = "personskatter",
            Title = "Taxes",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense
        },

        new()
        {
            Pattern = "parcelforening",
            Title = "Parcelforening",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        // =====================================
        // COMPANY EXPENSES
        // =====================================

        new()
        {
            Pattern = "proshop",
            Title = "Company Expense",
            Category = Category.Other,
            BudgetGroup = BudgetGroup.CompanyExpense
        },

        new()
        {
            Pattern = "elgiganten skejby",
            Title = "Company Electronics",
            Category = Category.Other,
            BudgetGroup = BudgetGroup.CompanyExpense
        },

        new()
        {
            Pattern = "jetbrains",
            Title = "JetBrains",
            Category = Category.Other,
            BudgetGroup = BudgetGroup.CompanyExpense
        },

        // =====================================
        // FOOD
        // =====================================

        new()
        {
            Pattern = "føtex",
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

        new()
        {
            Pattern = "lidl",
            Title = "Lidl",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "spar",
            Title = "Spar",
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
            Pattern = "meny",
            Title = "Meny",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "coop",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kiwi",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "salling stormagasin",
            Title = "Groceries",
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

        new()
        {
            Pattern = "ingo",
            Title = "Fuel",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

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
            Pattern = "epass24",
            Title = "Parking",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
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

        new()
        {
            Pattern = "dmr period afgift",
            Title = "Vehicle Registration",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.FixedExpense
        },

        // =====================================
        // HEALTH
        // =====================================

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
            Pattern = "sygeforsikringen danmark",
            Title = "Health Insurance",
            Category = Category.Health,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "fitnessunited",
            Title = "Fitness",
            Category = Category.Health,
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

        new()
        {
            Pattern = "openai",
            Title = "OpenAI",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "chatgpt",
            Title = "ChatGPT",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "bogshoppen",
            Title = "Books",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "joytunes",
            Title = "Music Learning",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "musikhuset aarhus",
            Title = "Culture",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "myticket",
            Title = "Tickets",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "holdet",
            Title = "Sports",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "spilforsyningen",
            Title = "Gaming",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        // =====================================
        // FOOD DELIVERY / RESTAURANTS
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

        new()
        {
            Pattern = "cafe vestergade",
            Title = "Restaurant",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "aeblehaven",
            Title = "Restaurant",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "bagergaarden",
            Title = "Bakery",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        // =====================================
        // SHOPPING
        // =====================================

        new()
        {
            Pattern = "normal",
            Title = "Everyday Shopping",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kattemad",
            Title = "Pets",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "apple com bill",
            Title = "Apple",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "one com",
            Title = "Hosting",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense
        },

        new()
        {
            Pattern = "hog hinnerup dk",
            Title = "Website",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense
        },

        new()
        {
            Pattern = "royaldckeys",
            Title = "Gaming",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "storm house",
            Title = "Shopping",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "skejby center",
            Title = "Shopping",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "tm materialer",
            Title = "Materials",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "squaretradecopay",
            Title = "Insurance Copay",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "vesterlund efterskol",
            Title = "School",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense
        },

        new()
        {
            Pattern = "kontoret",
            Title = "Office",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "elgiganten",
            Title = "Electronics",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "piet frisorsalon",
            Title = "Hairdresser",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "isager",
            Title = "Shopping",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kim mode",
            Title = "Clothing",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "hennes mauritz",
            Title = "Clothing",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "storv house",
            Title = "Shopping",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ecco",
            Title = "Shoes",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "trendhim",
            Title = "Accessories",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        // =====================================
        // OTHER
        // =====================================

        new()
        {
            Pattern = "stiftelsen idre",
            Title = "Donation",
            Category = Category.Other,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "depotsikring",
            Title = "Insurance",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense
        },

        new()
        {
            Pattern = "101 udbet fra 3fa",
            Title = "3F Refund",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        // =====================================
        // DEFAULT MOBILEPAY
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