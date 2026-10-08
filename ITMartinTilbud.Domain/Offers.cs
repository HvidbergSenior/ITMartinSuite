namespace ITMartinTilbud.Domain;

// One offer as the page shows it. UnitPrice is per kg, per litre or per piece, so a 3-pack and a single bag compare.
public sealed record Offer(
    string Chain, string Title, string? Description, decimal Price, decimal? NormalPrice,
    decimal? UnitPrice, string? UnitLabel, string? Size, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo,
    string? Image, string? Logo, bool Variant = false,
    string? NeedsApp = null, string? Condition = null, string? DealerId = null, string? NearestStore = null, double? NearestKm = null,
    string? AppName = null, decimal? AppPrice = null, decimal? NoAppPrice = null, bool Matches = true);
// Matches: has every describing word of the search ("økologisk" in "økologisk mælk"); false = the thing itself, not that kind.
// NeedsApp: the price shown is only for app users (Lidl Plus coupons, often). AppName + AppPrice: the price shown is for
// EVERYONE and the app gives this lower price on top (Netto "+ PRIS", føtex/Bilka "plus pris", Coop/MENY "medlemspris").
// NoAppPrice: what you pay without the app, when the shown price is an app price and the leaflet says the normal one.

/// <summary>The nearest store of a chain: address and distance in km.</summary>
public sealed record NearestStore(string Address, double Km);

/// <summary>The supermarkets Tilbud covers - Tjek also lists building centres, electronics and travel agents.</summary>
public static class GroceryChains
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Lidl", "Netto", "REMA 1000", "føtex", "Bilka", "Kvickly", "SuperBrugsen", "Brugsen", "Dagli'Brugsen", "MENY",
        "Løvbjerg", "SPAR", "Min Købmand", "LET-KØB", "365discount", "Coop 365", "ABC Lavpris", "Fakta", "Irma",
        "Nærkøb", "Salling", "Wolt Market", "Coop.dk MAD", "Lokal Bager", "Normal", "Basic & More", "Bilka ToGo",
    };

    public static bool Contains(string chain) => All.Contains(chain);
}

public static class Geo
{
    public const int MaxSearchKm = 50;

    /// <summary>Great-circle distance in km.</summary>
    public static double Km(double lat1, double lng1, double lat2, double lng2)
    {
        static double R(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin(R(lat2 - lat1) / 2), 2) + Math.Cos(R(lat1)) * Math.Cos(R(lat2)) * Math.Pow(Math.Sin(R(lng2 - lng1) / 2), 2);
        return 6371 * 2 * Math.Asin(Math.Sqrt(a));
    }
}

/// <summary>A problem to show the user, already in Danish.</summary>
public class TilbudException(string message) : Exception(message);
