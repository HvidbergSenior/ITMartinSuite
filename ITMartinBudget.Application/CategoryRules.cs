using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application;

public static class CategoryRules
{
    // Match ONLY complete words
    public static readonly HashSet<string> ExactWordRules =
    [
        "su",
        "ok",
        "q8",
        "dsb"
    ];

    public static readonly Dictionary<string, Category> Rules = new()
    {
        // =====================================
        // INCOME
        // =====================================

        ["løn"] = Category.Income,
        ["lønoverførsel"] = Category.Income,
        ["månedsløn"] = Category.Income,
        ["plusløn"] = Category.Income,

        ["su"] = Category.Income,

        ["pension"] = Category.Income,
        ["feriepenge"] = Category.Income,
        ["fk-feriepenge"] = Category.Income,
        ["bonus"] = Category.Income,
        ["bonustryghedsgruppen"] = Category.Income,
        ["udbetaling"] = Category.Income,
        ["overskydende skat"] = Category.Income,
        ["skat"] = Category.Income,
        ["danmark"] = Category.Income,

        // =====================================
        // FOOD
        // =====================================

        ["føtex"] = Category.Food,
        ["foetex"] = Category.Food,
        ["netto"] = Category.Food,
        ["rema"] = Category.Food,
        ["rema1000"] = Category.Food,
        ["lidl"] = Category.Food,
        ["kvickly"] = Category.Food,
        ["salling"] = Category.Food,
        ["meny"] = Category.Food,
        ["løvbjerg"] = Category.Food,
        ["brugsen"] = Category.Food,
        ["superbrugsen"] = Category.Food,
        ["bilka"] = Category.Food,

        ["wolt"] = Category.Food,
        ["justeat"] = Category.Food,
        ["sunset"] = Category.Food,
        ["mcdonald"] = Category.Food,
        ["burger king"] = Category.Food,
        ["skumhuset"] = Category.Food,

        // =====================================
        // TRANSPORT
        // =====================================

        ["circle k"] = Category.Transport,
        ["uno-x"] = Category.Transport,
        ["ok"] = Category.Transport,
        ["q8"] = Category.Transport,
        ["shell"] = Category.Transport,
        ["ingo"] = Category.Transport,
        ["benzin"] = Category.Transport,
        ["easypark"] = Category.Transport,
        ["parkering"] = Category.Transport,
        ["epass24"] = Category.Transport,
        ["rejsekort"] = Category.Transport,
        ["dsb"] = Category.Transport,
        ["brobizz"] = Category.Transport,

        // =====================================
        // BILLS
        // =====================================

        ["spotify"] = Category.Bills,
        ["netflix"] = Category.Bills,
        ["netflix.com"] = Category.Bills,

        ["telenor"] = Category.Bills,
        ["mobilepay telenor"] = Category.Bills,

        ["nrgi"] = Category.Bills,
        ["nrgi elhandel"] = Category.Bills,
        ["mobilepay nrgi elhandel"] = Category.Bills,

        ["de letsikr"] = Category.Bills,

        // =====================================
        // SHOPPING
        // =====================================

        ["about you"] = Category.Shopping,
        ["rivalxt"] = Category.Shopping,
        ["blue tomato"] = Category.Shopping,
        ["blue tomato gmbh"] = Category.Shopping,

        // =====================================
        // ENTERTAINMENT
        // =====================================

        ["steamgames"] = Category.Entertainment,
        ["steamgames.com"] = Category.Entertainment,

        // =====================================
        // HOUSING
        // =====================================

        ["termin jyske realkredit"] = Category.Housing,
        ["jyske realkredit"] = Category.Housing,
        ["mobilepay boligmontering"] = Category.Housing,

        // =====================================
        // TRAVEL
        // =====================================

        ["vesterlund-efterskol"] = Category.Travel,

        // =====================================
        // TRANSFER
        // =====================================

        ["mobilepay"] = Category.Transfer,

        ["bjørneo"] = Category.Transfer,
        ["børneo"] = Category.Transfer,

        ["mobilepay inge kjærulff torp"] = Category.Transfer,
        ["mobilepay bertil hvidberg john"] = Category.Transfer,
        ["mobilepay dorthe møller johnse"] = Category.Transfer,

        ["mobilepay julius hvidberg john"] = Category.Transfer,
        ["mobilepay marianne hvidberg"] = Category.Transfer,
        ["mobilepay sonja johnsen"] = Category.Transfer,
        ["mobilepay karl jon nielsen"] = Category.Transfer,
        ["mobilepay anne bro friis jense"] = Category.Transfer,

        // =====================================
        // HEALTH
        // =====================================

        ["silvan"] = Category.Housing,

        // =====================================
        // OTHER
        // =====================================

        ["sp alpex"] = Category.Other,
    };
}