using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Converters;

public class SubCategoryConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        var kategori = text?.Trim().ToLowerInvariant() ?? "";
        var tekst = row.GetField("Tekst")?.ToLowerInvariant() ?? "";

        // 🛒 Food
        if (kategori.Contains("dagligvarer"))
            return SubCategory.Dagligvarer;

        if (kategori.Contains("restaurant") || kategori.Contains("café"))
            return SubCategory.Restaurant;

        // ⛽ Transport
        if (kategori.Contains("brændstof"))
            return SubCategory.Benzin;

        if (kategori.Contains("parkering"))
            return SubCategory.Parkering;

        // 🏠 Housing
        if (kategori.Contains("realkredit"))
            return SubCategory.Husleje;

        if (kategori.Contains("bolig"))
            return SubCategory.Bolig;

        // 💰 Income
        if (kategori.Contains("løn"))
            return SubCategory.Løn;

        if (kategori.Contains("offentlige ydelser"))
            return SubCategory.SU;

        if (kategori.Contains("anden indtægt"))
            return SubCategory.AndenIndkomst;

        // 📱 Subscriptions
        if (kategori.Contains("telefon") || kategori.Contains("streaming"))
            return SubCategory.Abonnement;

        // 🏥 Health
        if (kategori.Contains("læge") || kategori.Contains("medicin"))
            return SubCategory.Sundhed;

        // 👕 Shopping
        if (kategori.Contains("tøj"))
            return SubCategory.Tøj;

        // 🎮 Leisure
        if (kategori.Contains("film") || kategori.Contains("musik"))
            return SubCategory.Underholdning;

        if (kategori.Contains("sport"))
            return SubCategory.Fritid;

        // 🔁 Fallback using description (important!)
        if (tekst.Contains("netto") || tekst.Contains("rema") || tekst.Contains("føtex"))
            return SubCategory.Dagligvarer;

        if (tekst.Contains("mcd") || tekst.Contains("burger"))
            return SubCategory.Restaurant;

        if (tekst.Contains("realkredit"))
            return SubCategory.Husleje;

        return SubCategory.Ukendt;
    }
}