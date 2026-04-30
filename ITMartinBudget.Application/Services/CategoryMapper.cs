using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public static class CategoryMapper
{
    public static Category Map(SubCategory sub) => sub switch
    {
        // 💰 INCOME
        SubCategory.Løn or
            SubCategory.SU or
            SubCategory.Feriepenge or
            SubCategory.OverskydendeSkat or
            SubCategory.Renter or
            SubCategory.Pengegaver
            => Category.Indkomst,

        // 🛒 FOOD
        SubCategory.Dagligvarer or
            SubCategory.Restaurant or
            SubCategory.Fastfood
            => Category.Mad,

        // 🚗 TRANSPORT
        SubCategory.Benzin or
            SubCategory.Parkering or
            SubCategory.OffentligTransport
            => Category.Transport,

        // 🏠 HOUSING
        SubCategory.Husleje or
            SubCategory.RenterHusLån
            => Category.Bolig,

        // 📱 SUBSCRIPTIONS
        SubCategory.Internet or
            SubCategory.StreamingTjenester
            => Category.Abonnement,

        // 🏥 HEALTH
        SubCategory.Medicin or
            SubCategory.Tandlæge
            => Category.Sundhed,

        // 🎮 LEISURE
        SubCategory.PersonligtForbrug or
            SubCategory.FitnessSport or
            SubCategory.Rejser
            => Category.Fritid,

        _ => Category.Andet
    };
}