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

        var combined = $"{kategori} {tekst}";

        // =========================
        // 🔥 TRANSFERS (TOP PRIORITY)
        // =========================
        if (combined.Contains("overfør") ||
            combined.Contains("opsparing") ||
            combined.Contains("opsparingskonto") ||
            combined.Contains("egen konto"))
        {
            return SubCategory.Overførsel;
        }

        // =========================
        // 💰 INCOME
        // =========================
        if (combined.Contains("løn") || combined.Contains("lønoverførsel"))
            return SubCategory.Løn;

        if (combined.Contains("su"))
            return SubCategory.Løn; // 👈 your rule: SU grouped with salary

        if (combined.Contains("skat") || combined.Contains("rente"))
            return SubCategory.Løn;

        // =========================
        // 🛒 FOOD
        // =========================
        if (combined.Contains("dagligvarer") ||
            combined.Contains("netto") ||
            combined.Contains("rema") ||
            combined.Contains("føtex"))
            return SubCategory.Dagligvarer;

        if (combined.Contains("restaurant") ||
            combined.Contains("café") ||
            combined.Contains("pizza") ||
            combined.Contains("burger") ||
            combined.Contains("mcd"))
            return SubCategory.Restaurant;

        // =========================
        // ⛽ TRANSPORT
        // =========================
        if (combined.Contains("brændstof") ||
            combined.Contains("shell") ||
            combined.Contains("circle k"))
            return SubCategory.Benzin;

        if (combined.Contains("parkering"))
            return SubCategory.Parkering;

        // =========================
        // 🏠 HOUSING
        // =========================
        if (combined.Contains("realkredit"))
            return SubCategory.Husleje;

        if (combined.Contains("bolig"))
            return SubCategory.Bolig;

        // =========================
        // 📱 SUBSCRIPTIONS
        // =========================
        if (combined.Contains("telefon") || combined.Contains("streaming"))
            return SubCategory.Abonnement;

        // =========================
        // 🏥 HEALTH
        // =========================
        if (combined.Contains("læge") || combined.Contains("medicin"))
            return SubCategory.Sundhed;

        // =========================
        // 👕 SHOPPING
        // =========================
        if (combined.Contains("tøj"))
            return SubCategory.Tøj;

        // =========================
        // 🎮 LEISURE
        // =========================
        if (combined.Contains("film") || combined.Contains("musik"))
            return SubCategory.Underholdning;

        if (combined.Contains("sport"))
            return SubCategory.Fritid;

        return SubCategory.Ukendt;
    }
}