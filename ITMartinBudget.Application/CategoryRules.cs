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
        // FIXED INCOME
        // =====================================

        ["løn"] = Category.FixedIncome,
        ["lønoverførsel"] = Category.FixedIncome,
        ["månedsløn"] = Category.FixedIncome,
        ["plusløn"] = Category.FixedIncome,

        // exact word only
        ["su"] = Category.FixedIncome,

        // =====================================
        // FIXED EXPENSES
        // =====================================

        ["hog-hinnerup"] = Category.FixedExpenses,
        ["hog-hinnerup.dk"] = Category.FixedExpenses,

        ["spotify"] = Category.FixedExpenses,

        ["netflix"] = Category.FixedExpenses,
        ["netflix.com"] = Category.FixedExpenses,

        ["google one"] = Category.FixedExpenses,
        ["google storage"] = Category.FixedExpenses,

        ["telenor"] = Category.FixedExpenses,
        ["mobilepay telenor"] = Category.FixedExpenses,

        ["bs kredsløb"] = Category.FixedExpenses,
        ["kredsløb"] = Category.FixedExpenses,

        ["nrgi"] = Category.FixedExpenses,
        ["nrgi elhandel"] = Category.FixedExpenses,
        ["mobilepay nrgi elhandel"] = Category.FixedExpenses,

        ["alka"] = Category.FixedExpenses,
        ["alka forsikring"] = Category.FixedExpenses,
        ["bs alka forsikring"] = Category.FixedExpenses,

        ["termin jyske realkredit"] = Category.FixedExpenses,
        ["jyske realkredit"] = Category.FixedExpenses,

        ["fitnessunited"] = Category.FixedExpenses,
        ["fitness united"] = Category.FixedExpenses,
        ["fitnessunited.dk"] = Category.FixedExpenses,

        ["dmr"] = Category.FixedExpenses,
        ["dmr period"] = Category.FixedExpenses,

        ["motorstyrelsen"] = Category.FixedExpenses,
        ["motoropkrævning"] = Category.FixedExpenses,
        ["skattestyrelsen motoropkrævning"] = Category.FixedExpenses,

        ["aarhus vand"] = Category.FixedExpenses,
        ["bs aarhus vand a/s"] = Category.FixedExpenses,

        ["suno"] = Category.FixedExpenses,
        ["suno ai"] = Category.FixedExpenses,

        ["allente"] = Category.FixedExpenses,
        ["allente danmark"] = Category.FixedExpenses,
        ["bs allente danmark"] = Category.FixedExpenses,

        ["playstation network"] = Category.FixedExpenses,
        ["ps network"] = Category.FixedExpenses,
        ["sony playstation"] = Category.FixedExpenses,

        ["letsikr"] = Category.FixedExpenses,
        ["depotsikring letsikr"] = Category.FixedExpenses,

        ["akademikernes a-kasse"] = Category.FixedExpenses,
        ["a-kasse"] = Category.FixedExpenses,
        ["bs akademikernes a-kasse"] = Category.FixedExpenses,

        ["socialpædagogernes landsforbund"] = Category.FixedExpenses,
        ["bs socialpædagogernes landsforbund"] = Category.FixedExpenses,

        ["one.com"] = Category.FixedExpenses,
        ["vdk one.com"] = Category.FixedExpenses,

        ["openai *chatgpt subscr"] = Category.FixedExpenses,
        ["chatgpt subscr"] = Category.FixedExpenses,

        ["3f-kontingent"] = Category.FixedExpenses,

        ["better psykiatri"] = Category.FixedExpenses,
        ["mobilepay bedre psykiatri"] = Category.FixedExpenses,

        ["jetbrains"] = Category.FixedExpenses,
        ["jetbrains s.r.o."] = Category.FixedExpenses,

        ["parcelforeningen"] = Category.FixedExpenses,

        // =====================================
        // FOOD
        // =====================================

        // MUST COME BEFORE "su"
        ["su fdb"] = Category.Food,

        ["fdb"] = Category.Food,

        ["føtex"] = Category.Food,
        ["foetex"] = Category.Food,
        ["føtex skejby"] = Category.Food,
        ["foetex skejby"] = Category.Food,
        ["dk føtex skejby"] = Category.Food,
        ["vdk foetex skejby"] = Category.Food,

        ["netto"] = Category.Food,
        ["netto lystrup"] = Category.Food,
        ["dk netto randersvej"] = Category.Food,
        ["vdk netto veri-center"] = Category.Food,
        ["dk netto veri-centret"] = Category.Food,

        ["rema"] = Category.Food,
        ["rema1000"] = Category.Food,
        ["rema 1000"] = Category.Food,
        ["rema1000 risskov"] = Category.Food,
        ["dk rema1000 risskov"] = Category.Food,
        ["dk rema1000 aarhus"] = Category.Food,

        ["lidl"] = Category.Food,
        ["dk lidl"] = Category.Food,
        ["vdk lidlvericenter"] = Category.Food,

        ["kvickly"] = Category.Food,
        ["salling"] = Category.Food,
        ["dk salling stormagasin"] = Category.Food,

        ["meny"] = Category.Food,

        ["løvbjerg"] = Category.Food,
        ["loevbjerg"] = Category.Food,
        ["loevbjerg supermarked"] = Category.Food,
        ["dk løvbjerg supermarked a/s"] = Category.Food,
        ["vdk loevbjerg supermarked a/s"] = Category.Food,

        ["brugsen"] = Category.Food,
        ["superbrugsen"] = Category.Food,
        ["superbrugs"] = Category.Food,
        ["superbrugs fdb"] = Category.Food,

        ["bilka"] = Category.Food,

        ["wolt"] = Category.Food,
        ["justeat"] = Category.Food,
        ["sunset"] = Category.Food,
        ["mcdonald"] = Category.Food,
        ["burger king"] = Category.Food,
        ["skumhuset"] = Category.Food,
        ["thaiplus"] = Category.Food,
        ["tgtg"] = Category.Food,

        // =====================================
        // TRANSPORT
        // =====================================

        ["circle k"] = Category.Transport,
        ["uno-x"] = Category.Transport,

        // exact word only
        ["ok"] = Category.Transport,
        ["q8"] = Category.Transport,

        ["ok benzin"] = Category.Transport,
        ["q8 service"] = Category.Transport,

        ["sejer"] = Category.Transport,
        ["sejer & søn"] = Category.Transport,
        ["sejr jensen auto"] = Category.Transport,

        ["shell"] = Category.Transport,
        ["ingo"] = Category.Transport,
        ["benzin"] = Category.Transport,

        ["easypark"] = Category.Transport,
        ["easypark a/s"] = Category.Transport,
        ["mobilepay easy park a/s"] = Category.Transport,

        ["parkering"] = Category.Transport,
        ["epass24"] = Category.Transport,

        ["rejsekort"] = Category.Transport,
        ["mobilepay rejsekort"] = Category.Transport,

        // exact word only
        ["dsb"] = Category.Transport,

        ["brobizz"] = Category.Transport,

        ["dk ok århus, skejby"] = Category.Transport,

        ["superspeed"] = Category.Transport,

        ["mekaniker"] = Category.Transport,
        ["autoværksted"] = Category.Transport,
        ["værksted"] = Category.Transport,
        ["autoservice"] = Category.Transport,

        // =====================================
        // SHOPPING
        // =====================================

        ["about you"] = Category.Shopping,
        ["rivalxt"] = Category.Shopping,
        ["blue tomato"] = Category.Shopping,
        ["blue tomato gmbh"] = Category.Shopping,

        // =====================================
        // HEALTH
        // =====================================

        ["apotek"] = Category.Health,
        ["skejby apotek"] = Category.Health,
        ["dk skejby apotek"] = Category.Health,

        // =====================================
        // ENTERTAINMENT
        // =====================================

        ["steamgames"] = Category.Entertainment,
        ["steamgames.com"] = Category.Entertainment,

        ["xbox"] = Category.Entertainment,
        ["nintendo"] = Category.Entertainment,

        // =====================================
        // HOUSING
        // =====================================

        ["silvan"] = Category.Housing,
        ["vvs-eksperten"] = Category.Housing,
        ["mobilepay boligmontering"] = Category.Housing,

        // =====================================
        // TRAVEL
        // =====================================

        ["vesterlund-efterskol"] = Category.Travel,

        // =====================================
        // SAVINGS
        // =====================================

        ["opsparingskonto"] = Category.Savings,
        ["til opsparingskonto"] = Category.Savings,
        ["børneopsparing"] = Category.Savings,
        ["børneopsparing - bertil"] = Category.Savings,

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

        ["mobilepay bent møller joh"] = Category.Transfer,
        ["mobilepay eigil hvidberg johns"] = Category.Transfer,
        ["mobilepay eigil hvidberg"] = Category.Transfer,

        ["vdk mob.pay*eigil hvidberg jo"] = Category.Transfer,
        ["vdk mob.pay*bertil hvidberg j"] = Category.Transfer,
        ["vdk mob.pay*bent moller johns"] = Category.Transfer,

        ["mobilepay dorte rindom jeppese"] = Category.Transfer,
        ["mobilepay mathias olin hv"] = Category.Transfer,

        ["til 7633"] = Category.Transfer,
        ["til 7633 8119308"] = Category.Transfer,
        ["til 7633 0008318157"] = Category.Transfer,

        // =====================================
        // BILLS
        // =====================================

        ["pbs"] = Category.Bills,
        ["betalingsservice"] = Category.Bills,

        ["faktura"] = Category.Bills,
        ["regning"] = Category.Bills,
        ["gebyr"] = Category.Bills,
        ["afgift"] = Category.Bills,

        // =====================================
        // TRANSFER-LIKE INCOME
        // =====================================

        ["pension"] = Category.Transfer,
        ["feriepenge"] = Category.Transfer,
        ["fk-feriepenge"] = Category.Transfer,
        ["bonus"] = Category.Transfer,
        ["bonustryghedsgruppen"] = Category.Transfer,
        ["udbetaling"] = Category.Transfer,
        ["overskydende skat"] = Category.Transfer,
        ["skat"] = Category.Transfer,

        // =====================================
        // OTHER
        // =====================================

        ["sp alpex"] = Category.Other,
        // =====================================
// FOOD
// =====================================

["spar"] = Category.Food,
["spar skejby"] = Category.Food,

["kiwi"] = Category.Food,
["best"] = Category.Food,

// =====================================
// HEALTH
// =====================================

["sygeforsikringen danmark"] = Category.Health,
["bs sygeforsikringen danmark"] = Category.Health,

["tandlæge"] = Category.Health,
["tandlaege"] = Category.Health,
["dk tandlaege i skejby centre"] = Category.Health,

// =====================================
// SHOPPING
// =====================================

["hennes & mauritz"] = Category.Shopping,
["hm"] = Category.Shopping,

["km mode"] = Category.Shopping,

["normal"] = Category.Shopping,
["normal a/s"] = Category.Shopping,

["elgiganten"] = Category.Shopping,
["proshop"] = Category.Shopping,

["apple.com/bill"] = Category.Shopping,
["google play"] = Category.Shopping,

["iexpert"] = Category.Shopping,
["squaretrade"] = Category.Shopping,

// =====================================
// ENTERTAINMENT
// =====================================

["story house egmont"] = Category.Entertainment,
["royalcdkeys"] = Category.Entertainment,

// =====================================
// TRANSPORT
// =====================================

["thansen"] = Category.Transport,
["apcoa"] = Category.Transport,
["apcoa flow"] = Category.Transport,

// =====================================
// FIXED EXPENSES
// =====================================

["fagligt fælles forbund"] = Category.FixedExpenses,
["bs fagligt fælles forbund"] = Category.FixedExpenses,

["parcelforening"] = Category.FixedExpenses,

// =====================================
// TRANSFER
// =====================================

["vdk mob.pay*sonja johnsen"] = Category.Transfer,
["vdk mob.pay*julius hvidberg j"] = Category.Transfer,
["vdk mob.pay*christian birkefe"] = Category.Transfer,

["overførsel"] = Category.Transfer,

// =====================================
// OTHER
// =====================================

["kattemad"] = Category.Other,
["tm-materialer"] = Category.Other,
["Fødevarecheck"] = Category.Other,

    };
}