namespace ITMartinVareinfo.Domain;

// The words both apps use. Vareinfo and Lager are separate apps with their own databases (2026-10-12 split), so each
// keeps its own copy and they meet only as text in the API - a new kind never breaks old rows on either side.

// What kind of thing it is.
public static class Kinds
{
    public const string Cd = "CD", Dvd = "DVD", BluRay = "Blu-ray", Magasin = "Magasin", AndersAnd = "Anders And-blad",
        Jumbobog = "Jumbobog", Tegneserie = "Tegneserie", Konsolspil = "Konsolspil", Bog = "Bog",
        Magic = "Magic-kort", Merch = "Merchandise", Andet = "Andet";

    public static readonly string[] All = [AndersAnd, Jumbobog, Tegneserie, Magasin, Bog, Cd, Dvd, BluRay, Konsolspil, Magic, Merch, Andet];

    public static string Icon(string kind) => kind switch
    {
        Cd => "🎵", Dvd or BluRay => "💿", Magasin => "📰", AndersAnd or Jumbobog => "🦆", Tegneserie => "💥",
        Konsolspil => "🎮", Bog => "📚", Magic => "🃏", Merch => "🧸", _ => "📦",
    };

    public static string Normalise(string? kind) =>
        All.FirstOrDefault(k => string.Equals(k, kind?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Andet;
}

public static class Conditions
{
    public static readonly string[] All = ["Som ny", "Meget god", "God", "Slidt", "Defekt"];
    public static string Normalise(string? c) =>
        All.FirstOrDefault(x => string.Equals(x, c?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "God";
}

// What a book's cover itself says about the edition (read with the pile, no extra AI call). "Mulig 1. udgave" puts it
// on Lager's kolofon list even before the price hunter has found anything.
public static class EditionHints
{
    public const string First = "Mulig 1. udgave", BookClub = "Bogklub", Cheap = "Billigbog";
    public static readonly string[] All = ["", First, BookClub, Cheap];
    public static string Normalise(string? h) =>
        All.FirstOrDefault(x => x != "" && string.Equals(x, h?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "";
}

// What was read off a cover or looked up by barcode - the product info Lager stores with the item.
public sealed record Found(string Kind, string Title, string Artist, string Series, string Number, int? Year,
    string Platform, string Condition, int Quantity, double Confidence, string Note, string Interest = "", string Barcode = "",
    decimal? PriceHint = null, string EditionHint = "");

// What the AI read on one kolofon photo (the back of the title page).
public sealed record KolofonReading(string? Isbn, string? Edition, int? Year, string? Publisher, string? Note);

// One photo of a pile (kept, so a wrong reading can be checked against the picture). Lager's items point to it by Id.
public sealed class Photo
{
    public int Id { get; set; }
    public string Path { get; set; } = "";       // relative to the data dir
    public int? BoxId { get; set; }              // Lager's box id at the time - for the record only
    public int Found { get; set; }
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
}

// A kolofon photo of one of Lager's books, waiting to be read or already read (up to 10 per AI call).
public sealed class KolofonShot
{
    public const int PerCall = 10;

    public int Id { get; set; }
    public int ItemId { get; set; }              // Lager's item id
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }        // null = waiting
    public string Result { get; set; } = "";     // what was read, as a line - shown on the kolofon page

    public void Read(KolofonReading? r, DateTime now)
    {
        ReadAt = now;
        Result = r is null ? "Kunne ikke læses"
            : string.Join(" · ", new[] { r.Edition, r.Publisher, r.Year?.ToString(), r.Isbn is { Length: > 0 } i ? "ISBN " + i : null, r.Note }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}

// AI calls per day - the hard cap on spend lives in the database, so a restart does not reset it.
public sealed class AiDay
{
    public DateOnly Day { get; set; }
    public int Calls { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
}
