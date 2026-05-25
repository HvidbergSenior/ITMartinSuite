using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class EntertainmentRules
{
    public static readonly List<TransactionRule> Items =
    [
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
            Pattern = "steam",
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
            Pattern = "joytunes",
            Title = "Music Learning",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "tickets",
            Title = "Tickets",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
        Pattern = "stm sport",
        Title = "Sports",
        Category = Category.Entertainment,
        BudgetGroup = BudgetGroup.VariableExpense
        },
        new()
        {
            Pattern = "apple com bill",
            Title = "Apple",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "dtd group",
            Title = "NorthSide",
            Category = Category.Northside,
            BudgetGroup = BudgetGroup.VariableExpense
        },
        new()
        {
            Pattern = "ilva",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "klitmoeller",
            Title = "Entertainment",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kreativ kerami",
            Title = "Creative Activity",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "umashi aarhus",
            Title = "Restaurant",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "piet",
            Title = "Hairdresser",
            Category = Category.Shopping,
            BudgetGroup = BudgetGroup.VariableExpense
        },
        new()
        {
            Pattern = "mcdonald",
            Title = "Fast Food",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "chokolet",
            Title = "Snacks & Treats",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kop og kande",
            Title = "Lifestyle",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "noeddebutikken",
            Title = "Snacks & Treats",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
{
    Pattern = "butikk",
    Title = "Lifestyle",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "cafe",
    Title = "Cafe",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "restaurant",
    Title = "Restaurant",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "bio",
    Title = "Cinema",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "cinemaxx",
    Title = "Cinema",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "paradis",
    Title = "Cinema",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "ticket",
    Title = "Tickets",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "festival",
    Title = "Festival",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "steam",
    Title = "Gaming",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "playstation",
    Title = "Gaming",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "google play",
    Title = "Apps & Media",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "apple",
    Title = "Apps & Media",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "spotify",
    Title = "Music",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "netflix",
    Title = "Streaming",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "universal music",
    Title = "Music",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "musik",
    Title = "Music",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "keramik",
    Title = "Creative",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "kreativ",
    Title = "Creative",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "genbrug",
    Title = "Second Hand",
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
    Pattern = "klitmoeller",
    Title = "Experience",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
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
    Pattern = "umashi",
    Title = "Restaurant",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},new()
{
    Pattern = "fof aarhus",
    Title = "Courses & Culture",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "paradisbio",
    Title = "Cinema",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "oest for paradis",
    Title = "Cinema",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "casino bar",
    Title = "Restaurant & Bar",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "bruuns galleri",
    Title = "Shopping & Lifestyle",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "hyldedeluxe",
    Title = "Lifestyle",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "ibens kaffehus",
    Title = "Cafe",
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
    Pattern = "google play",
    Title = "Apps & Media",
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

new()
{
    Pattern = "holdet",
    Title = "Sports & Leisure",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "ideal of sweden",
    Title = "Lifestyle",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "2nddeluxe",
    Title = "Second Hand",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "blue tomato",
    Title = "Lifestyle",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "paw sko",
    Title = "Lifestyle",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "shopping4net",
    Title = "Lifestyle",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "ecco",
    Title = "Lifestyle",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "apple com bill",
    Title = "Apple Services",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "klarna",
    Title = "Lifestyle Purchase",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "trendhim",
    Title = "Lifestyle",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "google play apps",
    Title = "Apps & Media",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "kongsvingervej 1",
    Title = "Leisure",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "ejvinds stormgade",
    Title = "Restaurant",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},

new()
{
    Pattern = "butler loftet",
    Title = "Restaurant",
    Category = Category.Entertainment,
    BudgetGroup = BudgetGroup.VariableExpense
},new()
        {
            Pattern = "aeblehaven",
            Title = "Restaurant",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "ubisoft",
            Title = "Gaming",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rito",
            Title = "Creative Hobby",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "malerfirma tidens farver",
            Title = "Home / Creative",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rivalxt",
            Title = "Lifestyle",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "only stores",
            Title = "Clothing",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "peter gift",
            Title = "Gift",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "reshopit",
            Title = "Second Hand",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "julie sandlau",
            Title = "Jewelry",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "radisson blu",
            Title = "Hotel / Experience",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "thaiplus",
            Title = "Restaurant",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "skumhuset",
            Title = "Cafe / Leisure",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "taxas",
            Title = "Restaurant",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sportmaster",
            Title = "Sports & Lifestyle",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "lagkagehuset",
            Title = "Bakery & Cafe",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sams pita",
            Title = "Restaurant",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "roede kors butik",
            Title = "Second Hand",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "starbucks",
            Title = "Cafe",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "mft knitwear",
            Title = "Clothing",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "thansen",
            Title = "Car & Hobby",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rente",
            Title = "Interest",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.VariableIncome,
            TransactionType = TransactionType.Indkomst
        },new()
        {
            Pattern = "modekompagniet",
            Title = "Clothing",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "danmark",
            Title = "Insurance Refund",
            Category = Category.Income,
            BudgetGroup = BudgetGroup.VariableIncome,
            TransactionType = TransactionType.Indkomst
        },

        new()
        {
            Pattern = "3627 11254691",
            Title = "Internal Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "zooplus",
            Title = "Pets",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "lyko",
            Title = "Beauty",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kop kande",
            Title = "Lifestyle",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "mcdrandersvej",
            Title = "Fast Food",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "skechers",
            Title = "Shoes",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sejr jensen auto",
            Title = "Car Repair",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "kasses skovpoelser",
            Title = "Food Stall",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "sktst motor",
            Title = "Vehicle Tax",
            Category = Category.Transport,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "saxo",
            Title = "Books",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },new()
        {
            Pattern = "way ahead group",
            Title = "Consulting / Service",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "6180 17682091",
            Title = "Internal Transfer",
            Category = Category.Transfer,
            BudgetGroup = BudgetGroup.InternalTransfer
        },

        new()
        {
            Pattern = "vesterlund efterskol",
            Title = "School",
            Category = Category.Bills,
            BudgetGroup = BudgetGroup.FixedExpense,
            IsRecurring = true
        },

        new()
        {
            Pattern = "kjaer sommerfeldt",
            Title = "Wine",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "rasses skovpoelser",
            Title = "Food Stall",
            Category = Category.Entertainment,
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
            Pattern = "reenberg groent",
            Title = "Groceries",
            Category = Category.Food,
            BudgetGroup = BudgetGroup.VariableExpense
        },

        new()
        {
            Pattern = "reshoppit",
            Title = "Second Hand",
            Category = Category.Entertainment,
            BudgetGroup = BudgetGroup.VariableExpense
        },
    ];
}