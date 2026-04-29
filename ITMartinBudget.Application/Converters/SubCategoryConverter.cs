using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using ITMartinBudget.Domain.Enums;
using System.Globalization;

namespace ITMartinBudget.Application.Converters;

public class SubCategoryConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
{
    var kategori = text?.Trim().ToLowerInvariant() ?? "";
    var tekst = row.GetField("Tekst")?.ToLowerInvariant() ?? "";

    var amountText = row.GetField("Beløb");
    decimal.TryParse(amountText, NumberStyles.Any, new CultureInfo("da-DK"), out var amount);

    var combined = $"{kategori} {tekst}";

    // 🔁 TRANSFERS (ONLY real transfers)
    if (combined.Contains("overfør") ||
        combined.Contains("egen konto") ||
        combined.Contains("konto til konto"))
    {
        return amount >= 0
            ? SubCategory.OverførselFraAndre
            : SubCategory.OverførselTilAndre;
    }

    // 💾 SAVINGS (NOT transfer!)
    if (combined.Contains("opsparing"))
        return SubCategory.Opsparing;

    if (combined.Contains("børneopsparing"))
        return SubCategory.Børneopsparing;

    // 💰 INCOME
    if (combined.Contains("su"))
        return SubCategory.SU;

    if (combined.Contains("skat"))
        return SubCategory.OverskydendeSkat;

    if (combined.Contains("rente"))
        return SubCategory.Renter;

    if (combined.Contains("løn"))
        return SubCategory.Løn;

    if (combined.Contains("gave"))
        return amount >= 0
            ? SubCategory.Pengegaver
            : SubCategory.GaverTilAndre;

    // 🛒 FOOD
    if (combined.Contains("netto") || combined.Contains("rema") || combined.Contains("føtex") || combined.Contains("bilka"))
        return SubCategory.Dagligvarer;

    if (combined.Contains("restaurant") || combined.Contains("café") || combined.Contains("pizza") ||
        combined.Contains("burger") || combined.Contains("wolt") || combined.Contains("just eat"))
        return SubCategory.Restaurant;

    if (combined.Contains("slik") || combined.Contains("snack"))
        return SubCategory.Snacks;

    // 🚗 TRANSPORT
    if (combined.Contains("shell") || combined.Contains("circle k") || combined.Contains("ok tank"))
        return SubCategory.Benzin;

    if (combined.Contains("parkering"))
        return SubCategory.Parkering;

    if (combined.Contains("dsb") || combined.Contains("tog") || combined.Contains("bus"))
        return SubCategory.OffentligTransport;

    if (combined.Contains("værksted") || combined.Contains("mekaniker"))
        return SubCategory.ReparationBil;

    // 🏠 HOUSING
    if (combined.Contains("realkredit") || combined.Contains("lån"))
        return SubCategory.RenterLån;

    if (combined.Contains("husleje"))
        return SubCategory.Husleje;

    if (combined.Contains("kredsløb") || combined.Contains("vand") || combined.Contains("varme"))
        return SubCategory.VarmeVandAffald;

    if (combined.Contains("håndværker"))
        return SubCategory.ReparationHus;

    // 📱 SUBSCRIPTIONS
    if (combined.Contains("spotify") || combined.Contains("netflix") || combined.Contains("youtube"))
        return SubCategory.StreamingTjenester;

    if (combined.Contains("internet"))
        return SubCategory.Internet;

    if (combined.Contains("telefon"))
        return SubCategory.Telefonabonnement;

    // 🏥 HEALTH
    if (combined.Contains("tandlæge"))
        return SubCategory.Tandlæge;

    if (combined.Contains("sygeforsikring"))
        return SubCategory.Sygeforsikring;

    if (combined.Contains("medicin") || combined.Contains("apotek"))
        return SubCategory.Medicin;

    // 👕 SHOPPING
    if (combined.Contains("tøj"))
        return SubCategory.Tøj;

    // 🎮 LEISURE
    if (combined.Contains("biograf") || combined.Contains("film") || combined.Contains("musik"))
        return SubCategory.Underholdning;

    if (combined.Contains("fitness") || combined.Contains("sport"))
        return SubCategory.FitnessSport;

    if (combined.Contains("hotel") || combined.Contains("fly"))
        return SubCategory.Rejser;

    // ✂️ PERSONAL
    if (combined.Contains("frisør"))
        return SubCategory.Frisør;

    if (combined.Contains("pleje"))
        return SubCategory.PersonligPleje;

    return SubCategory.Ukendt;
}
}