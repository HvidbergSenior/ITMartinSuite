using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public static class CategoryMapper
{
    public static Category Map(SubCategory sub) => sub switch
    {
        // 💰 INCOME
        SubCategory.Løn or
        SubCategory.SU or
        SubCategory.OverskydendeSkat or
        SubCategory.Renter or
        SubCategory.Pengegaver or
        SubCategory.VirksomhedsIndkomst
            => Category.Indkomst,

        // 🔁 TRANSFERS (ignored in budget)
        SubCategory.OverførselFraAndre or
        SubCategory.OverførselTilAndre
            => Category.Andet,

        // 🛒 FOOD
        SubCategory.Dagligvarer or
        SubCategory.Restaurant or
        SubCategory.Snacks
            => Category.Mad,

        // 🚗 TRANSPORT
        SubCategory.Benzin or
        SubCategory.Parkering or
        SubCategory.ReparationBil or
        SubCategory.OffentligTransport
            => Category.Transport,

        // 🏠 HOUSING
        SubCategory.Husleje or
        SubCategory.RenterLån or
        SubCategory.VarmeVandAffald or
        SubCategory.ReparationHus
            => Category.Bolig,

        // 📱 SUBSCRIPTIONS
        SubCategory.Telefonabonnement or
        SubCategory.Internet or
        SubCategory.StreamingTjenester
            => Category.Abonnement,

        // 🏥 HEALTH
        SubCategory.Tandlæge or
        SubCategory.Sygeforsikring or
        SubCategory.Medicin
            => Category.Sundhed,

        // 👕 SHOPPING
        SubCategory.Tøj
            => Category.Shopping,

        // 🎮 LEISURE
        SubCategory.Underholdning or
        SubCategory.FitnessSport or
        SubCategory.Rejser
            => Category.Fritid,

        // 💾 SAVINGS + FINANCE + PERSONAL → general expense bucket
        SubCategory.Opsparing or
        SubCategory.Børneopsparing or
        SubCategory.FagforeningAkasse or
        SubCategory.Forsikring or
        SubCategory.VirksomhedsUdgift or
        SubCategory.Frisør or
        SubCategory.PersonligPleje or
        SubCategory.GaverTilAndre
            => Category.Andet,

        _ => Category.Andet
    };
}