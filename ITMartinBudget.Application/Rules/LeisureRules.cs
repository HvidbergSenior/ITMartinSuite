using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class LeisureRules
{
    public static readonly List<TransactionRule> Items =
    [
        Entertainment(
            "universal music",
            "Universal Music",
            Category.Fritid,
            ComparingType.Contains),

        Entertainment(
            "fof aarhus",
            "FOF Aarhus",
            Category.Fritid,
            ComparingType.Contains),

        RejserUdflugter(
            "klitmoeller",
            "Klitmøller",
            Category.RejserUdflugter,
            ComparingType.Contains),

        RestaurantCafe(
            "radisson blu",
            "Radisson Blu",
            Category.Cafe,
            ComparingType.Contains),

        HomeRepair(
            "malerfirma tidens farver",
            "Tidens Farver",
            Category.BoligVedligehold,
            ComparingType.Contains),

        GeneralShopping(
            "skumhuset",
            "Skumhuset",
            Category.Fritid,
            ComparingType.Contains),

        RestaurantCafe(
            "chokolet",
            "Chokolet",
            Category.Cafe,
            ComparingType.Contains),

        RestaurantCafe(
            "noeddebutikken",
            "Nøddebutikken",
            Category.Cafe,
            ComparingType.Contains),

        GeneralShopping(
            "roede kors butik",
            "Røde Kors Butik",
            Category.Toej,
            ComparingType.Contains),

        PaymentForChildren(
            "vesterlund efterskol",
            "Vesterlund Efterskole",
            Category.Boern,
            ComparingType.Contains),

        Entertainment(
            "fastelavnsbazar",
            "Fastelavnsbazar",
            Category.Fritid,
            ComparingType.Contains),
        
        Entertainment(
        "zettle escapist",
        "Escapist",
        Category.Fritid,
        ComparingType.Contains),
        
        GeneralShopping(
            "stofshop abyhoj",
            "Stofshop",
            Category.BoligVedligehold,
            ComparingType.Contains),

        GeneralShopping(
            "skive tek",
            "Skive Tek",
            Category.Fritid,
            ComparingType.Contains),

        GeneralShopping(
            "noddebutikken",
            "Nøddebutikken",
            Category.Bolig,
            ComparingType.Contains),

        GeneralShopping(
            "kjaer sommerfeldt",
            "Kjær Sommerfeldt",
            Category.Bolig,
            ComparingType.Contains),

        GeneralShopping(
            "the way ahead group",
            "The Way Ahead Group",
            Category.Bolig,
            ComparingType.Contains),

        GeneralShopping(
            "drandersvej",
            "Drandersvej",
            Category.Bolig,
            ComparingType.Contains),
        
        PaymentForChildren(
            "gallafest",
            "Gallafest",
            Category.Fritid,
            ComparingType.Contains),

        Entertainment(
            "fastelavsbazar",
            "Fastelavsbazar",
            Category.Fritid,
            ComparingType.Contains),


        RulesFactory.GeneralShopping(
            "sp alpex",
            "SP Alpex",
            Category.Boern,
            ComparingType.Contains),

        RulesFactory.GeneralShopping(
            "dk isager",
            "Isager",
            Category.OtherThanGroceries,
            ComparingType.Exact),

        RulesFactory.GeneralShopping(
            "dk kop kande web aps",
            "Kop & Kande",
            Category.Bolig,
            ComparingType.Exact),
        RulesFactory.PaymentForChildren(
            "dk surfline aps",
            "Surfline",
            Category.RejserUdflugter,
            ComparingType.Exact),
        RulesFactory.GeneralShopping(
            "mobilepay old boys45",
            "Old Boys",
            Category.Fritid,
            ComparingType.Exact),
        RulesFactory.GeneralShopping(
            "mobilepay tattoo fashion skive",
            "Tattoo Fashion",
            Category.OtherThanGroceries,
            ComparingType.Exact),

        RulesFactory.GeneralShopping(
            "tm materialer",
            "TM Materialer",
            Category.OtherThanGroceries,
            ComparingType.Exact),
        RulesFactory.GeneralShopping(
            "vdk bogshoppen",
            "Bogshoppen",
            Category.OtherThanGroceries,
            ComparingType.Exact),

        RulesFactory.GeneralShopping(
            "vdk dfp193453411",
            "DFP",
            Category.OtherThanGroceries,
            ComparingType.Exact),
        RulesFactory.RejserUdflugter(
            "vdk idre fja ll 18",
            "Idre Fjäll",
            Category.Ferie,
            ComparingType.Exact),
        RulesFactory.GeneralShopping(
            "vdk iexpert aps",
            "IExpert",
            Category.OtherThanGroceries,
            ComparingType.Exact),

        RulesFactory.GeneralShopping(
            "vdk inntq ab",
            "INNTQ",
            Category.OtherThanGroceries,
            ComparingType.Exact),

        RulesFactory.GeneralShopping(
            "vdk kontoret",
            "Kontoret",
            Category.OtherThanGroceries,
            ComparingType.Exact),
        RulesFactory.RejserUdflugter(
            "vdk stiftelsen idre",
            "Idre",
            Category.Ferie,
            ComparingType.Exact),
    ];
}