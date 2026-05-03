using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Converters;

public class SubCategoryConverter : DefaultTypeConverter, ISubCategoryConverter
{
    public SubCategory Convert(IReaderRow row)
    {
        var hovedkategori = row.GetField("Hovedkategori")?.ToLowerInvariant() ?? "";
        var kategori = row.GetField("Kategori")?.ToLowerInvariant() ?? "";
        var tekst = row.GetField("Tekst")?.ToLowerInvariant() ?? "";

        return Detect(hovedkategori, kategori, tekst);
    }

    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        return Convert(row);
    }

    private static SubCategory Detect(string hovedkategori, string kategori, string tekst)
    {
        var combined = $"{hovedkategori} {kategori} {tekst}";

        if (tekst.StartsWith("til ") || tekst.StartsWith("fra ") || combined.Contains("overfør"))
            return SubCategory.Kontooverførsel;

        if (hovedkategori.Contains("opsparing"))
            return kategori.Contains("børne")
                ? SubCategory.Børneopsparing
                : SubCategory.Opsparing;

        if (hovedkategori.Contains("indtægt"))
        {
            if (kategori.Contains("løn")) return SubCategory.Løn;
            if (kategori.Contains("skat")) return SubCategory.OverskydendeSkat;
            return SubCategory.Pengegaver;
        }

        if (hovedkategori.Contains("mad"))
        {
            if (kategori.Contains("restaurant")) return SubCategory.Restaurant;
            if (kategori.Contains("fastfood")) return SubCategory.Fastfood;
            return SubCategory.Dagligvarer;
        }

        if (hovedkategori.Contains("transport"))
        {
            if (kategori.Contains("brændstof")) return SubCategory.Benzin;
            if (kategori.Contains("parkering")) return SubCategory.Parkering;
            return SubCategory.OffentligTransport;
        }

        if (hovedkategori.Contains("bolig"))
        {
            if (kategori.Contains("realkredit")) return SubCategory.RenterHusLån;
            return SubCategory.Husleje;
        }

        if (hovedkategori.Contains("medier"))
        {
            if (tekst.Contains("spotify") || tekst.Contains("netflix"))
                return SubCategory.StreamingTjenester;

            if (tekst.Contains("internet") || tekst.Contains("one.com"))
                return SubCategory.Internet;

            return SubCategory.StreamingTjenester;
        }

        if (kategori.Contains("medicin") || tekst.Contains("apotek"))
            return SubCategory.Medicin;

        if (tekst.Contains("tandlæge"))
            return SubCategory.Tandlæge;

        if (hovedkategori.Contains("tøj"))
            return SubCategory.Tøj;

        if (hovedkategori.Contains("fritid"))
        {
            if (kategori.Contains("sport")) return SubCategory.FitnessSport;
            if (kategori.Contains("rejse")) return SubCategory.Rejser;
            return SubCategory.PersonligtForbrug;
        }

        if (hovedkategori.Contains("forsikring"))
            return SubCategory.Forsikring;

        if (kategori.Contains("fagforening"))
            return SubCategory.FagforeningAkasse;

        return SubCategory.Ukendt;
    }
}