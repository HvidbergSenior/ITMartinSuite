using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Converters;

public class SubCategoryConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        var hovedkategori = row.GetField("Hovedkategori")?.ToLowerInvariant() ?? "";
        var kategori = row.GetField("Kategori")?.ToLowerInvariant() ?? "";
        var tekst = row.GetField("Tekst")?.ToLowerInvariant() ?? "";

        var combined = $"{hovedkategori} {kategori} {tekst}";

        // 🔁 TRANSFER
        if (tekst.StartsWith("til ") || tekst.StartsWith("fra ") || combined.Contains("overfør"))
            return SubCategory.Kontooverførsel;

        // 💾 SAVINGS
        if (hovedkategori.Contains("opsparing"))
        {
            if (kategori.Contains("børne"))
                return SubCategory.Børneopsparing;

            return SubCategory.Opsparing;
        }

        // 💰 INCOME
        if (hovedkategori.Contains("indtægt") || hovedkategori.Contains("indtægter"))
        {
            if (kategori.Contains("løn"))
                return SubCategory.Løn;

            if (kategori.Contains("skat"))
                return SubCategory.OverskydendeSkat;

            return SubCategory.Pengegaver;
        }

        // 🛒 FOOD
        if (hovedkategori.Contains("mad"))
        {
            if (kategori.Contains("restaurant"))
                return SubCategory.Restaurant;

            if (kategori.Contains("fastfood"))
                return SubCategory.Fastfood;

            return SubCategory.Dagligvarer;
        }

        // 🚗 TRANSPORT
        if (hovedkategori.Contains("transport"))
        {
            if (kategori.Contains("brændstof"))
                return SubCategory.Benzin;

            if (kategori.Contains("parkering"))
                return SubCategory.Parkering;

            return SubCategory.OffentligTransport;
        }

        // 🏠 HOUSING
        if (hovedkategori.Contains("bolig"))
        {
            if (kategori.Contains("realkredit"))
                return SubCategory.RenterHusLån;

            return SubCategory.Husleje;
        }

        // 📱 MEDIA
        if (hovedkategori.Contains("medier"))
        {
            if (tekst.Contains("spotify") || tekst.Contains("netflix"))
                return SubCategory.StreamingTjenester;

            if (tekst.Contains("internet") || tekst.Contains("one.com"))
                return SubCategory.Internet;

            return SubCategory.StreamingTjenester;
        }

        // 🏥 HEALTH
        if (kategori.Contains("medicin") || tekst.Contains("apotek"))
            return SubCategory.Medicin;

        if (tekst.Contains("tandlæge"))
            return SubCategory.Tandlæge;

        // 👕 SHOPPING
        if (hovedkategori.Contains("tøj"))
            return SubCategory.Tøj;

        // 🎮 LEISURE
        if (hovedkategori.Contains("fritid"))
        {
            if (kategori.Contains("sport"))
                return SubCategory.FitnessSport;

            if (kategori.Contains("rejse"))
                return SubCategory.Rejser;

            return SubCategory.PersonligtForbrug;
        }

        // 📊 FINANCE
        if (hovedkategori.Contains("forsikring"))
            return SubCategory.Forsikring;

        if (kategori.Contains("fagforening"))
            return SubCategory.FagforeningAkasse;

        return SubCategory.Ukendt;
    }
}