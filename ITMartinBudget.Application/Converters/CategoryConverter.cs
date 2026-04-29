using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Converters;

public class CategoryConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        // 🔥 IMPORTANT:
        // Reuse SubCategory logic instead of duplicating rules

        var subConverter = new SubCategoryConverter();

        var sub = (SubCategory)subConverter.ConvertFromString(text, row, memberMapData);

        return sub switch
        {
            // 💰 INCOME (merged)
            SubCategory.Løn => Category.Indkomst,

            // 🔁 TRANSFERS
            SubCategory.Overførsel => Category.Andet,

            // 🛒 FOOD
            SubCategory.Dagligvarer or SubCategory.Restaurant
                => Category.Mad,

            // ⛽ TRANSPORT
            SubCategory.Benzin or SubCategory.Parkering
                => Category.Transport,

            // 🏠 HOUSING
            SubCategory.Husleje or SubCategory.Bolig
                => Category.Bolig,

            // 📱 SUBSCRIPTIONS
            SubCategory.Abonnement
                => Category.Abonnement,

            // 🏥 HEALTH
            SubCategory.Sundhed
                => Category.Sundhed,

            // 👕 SHOPPING
            SubCategory.Tøj
                => Category.Shopping,

            // 🎮 LEISURE
            SubCategory.Underholdning or SubCategory.Fritid
                => Category.Fritid,

            _ => Category.Andet
        };
    }
}