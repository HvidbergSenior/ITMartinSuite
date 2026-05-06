using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application;

public static class CategoryRules
{
    public static readonly Dictionary<string, Category> Rules = new()
    {
        // =====================================
        // FOOD
        // =====================================

        ["netto"] = Category.Food,
        ["rema"] = Category.Food,
        ["rema1000"] = Category.Food,
        ["føtex"] = Category.Food,
        ["bilka"] = Category.Food,
        ["lidl"] = Category.Food,
        ["aldi"] = Category.Food,
        ["brugsen"] = Category.Food,
        ["meny"] = Category.Food,
        ["spar"] = Category.Food,
        ["365discount"] = Category.Food,
        ["løvbjerg"] = Category.Food,
        ["wolt"] = Category.Food,
        ["justeat"] = Category.Food,
        ["mcdonald"] = Category.Food,
        ["burger king"] = Category.Food,
        ["sunset"] = Category.Food,
        ["skumhuset"] = Category.Food,

        // =====================================
        // TRANSPORT
        // =====================================

        ["ok benzin"] = Category.Transport,
        ["circle k"] = Category.Transport,
        ["q8"] = Category.Transport,
        ["shell"] = Category.Transport,
        ["dsb"] = Category.Transport,
        ["fynbus"] = Category.Transport,
        ["rejsekort"] = Category.Transport,
        ["parkering"] = Category.Transport,
        ["brobizz"] = Category.Transport,

        // =====================================
        // SHOPPING
        // =====================================

        ["amazon"] = Category.Shopping,
        ["zalando"] = Category.Shopping,
        ["about you"] = Category.Shopping,
        ["blue tomato"] = Category.Shopping,
        ["hm"] = Category.Shopping,
        ["h&m"] = Category.Shopping,
        ["ikea"] = Category.Shopping,
        ["elgiganten"] = Category.Shopping,
        ["power"] = Category.Shopping,
        ["proshop"] = Category.Shopping,

        // =====================================
        // ENTERTAINMENT
        // =====================================

        ["steam"] = Category.Entertainment,
        ["steamgames"] = Category.Entertainment,
        ["playstation"] = Category.Entertainment,
        ["xbox"] = Category.Entertainment,
        ["netflix"] = Category.Entertainment,
        ["spotify"] = Category.Entertainment,
        ["disney"] = Category.Entertainment,
        ["youtube"] = Category.Entertainment,
        ["viaplay"] = Category.Entertainment,
        ["tivoli"] = Category.Entertainment,

        // =====================================
        // HOUSING
        // =====================================

        ["realkredit"] = Category.Housing,
        ["jyske realkredit"] = Category.Housing,
        ["husleje"] = Category.Housing,
        ["bolig"] = Category.Housing,
        ["ejendom"] = Category.Housing,

        // =====================================
        // BILLS
        // =====================================

        ["norlys"] = Category.Bills,
        ["telia"] = Category.Bills,
        ["telenor"] = Category.Bills,
        ["3 mobil"] = Category.Bills,
        ["yousee"] = Category.Bills,
        ["ewii"] = Category.Bills,
        ["verdo"] = Category.Bills,
        ["vand"] = Category.Bills,
        ["el"] = Category.Bills,
        ["internet"] = Category.Bills,

        // =====================================
        // HEALTH
        // =====================================

        ["apotek"] = Category.Health,
        ["læge"] = Category.Health,
        ["tandlæge"] = Category.Health,
        ["frisør"] = Category.Health,
        ["piet frisørsalon"] = Category.Health,
        ["fitness"] = Category.Health,

        // =====================================
        // TRAVEL
        // =====================================

        ["sas"] = Category.Travel,
        ["ryanair"] = Category.Travel,
        ["booking.com"] = Category.Travel,
        ["airbnb"] = Category.Travel,
        ["united tickets"] = Category.Travel,
        ["hotel"] = Category.Travel,

        // =====================================
        // INCOME
        // =====================================

        ["løn"] = Category.Income,
        ["månedsløn"] = Category.Income,
        ["lønoverførsel"] = Category.Income,
        ["pension"] = Category.Income,
        ["feriepenge"] = Category.Income,
        ["skat"] = Category.Income,
        ["su"] = Category.Income,

        // =====================================
        // SAVINGS
        // =====================================

        ["opsparing"] = Category.Savings,
        ["savings"] = Category.Savings,

        // =====================================
        // TRANSFER
        // =====================================

        ["mobilepay overførsel"] = Category.Transfer,
        ["intern overførsel"] = Category.Transfer,
        ["egen konto"] = Category.Transfer,
        ["kontooverførsel"] = Category.Transfer
    };
}