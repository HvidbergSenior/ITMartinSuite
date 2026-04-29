using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public static class CategoryMapper
{
    public static Category Map(SubCategory sub) => sub switch
    {
        SubCategory.Dagligvarer or SubCategory.Restaurant
            => Category.Mad,

        SubCategory.Benzin or SubCategory.Parkering
            => Category.Transport,

        SubCategory.Husleje or SubCategory.Bolig
            => Category.Bolig,

        SubCategory.Abonnement
            => Category.Abonnement,

        SubCategory.Sundhed
            => Category.Sundhed,

        SubCategory.Tøj
            => Category.Shopping,

        SubCategory.Underholdning or SubCategory.Fritid
            => Category.Fritid,

        _ => Category.Andet
    };
}