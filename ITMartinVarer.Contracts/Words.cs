namespace ITMartinVarer.Contracts;

// The words every app uses about a thing. Stored as text everywhere, so a new kind never breaks old rows.

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
// on the kolofon list even before the price hunter has found anything.
public static class EditionHints
{
    public const string First = "Mulig 1. udgave", BookClub = "Bogklub", Cheap = "Billigbog";
    public static readonly string[] All = ["", First, BookClub, Cheap];
    public static string Normalise(string? h) =>
        All.FirstOrDefault(x => x != "" && string.Equals(x, h?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "";
}

// Box codes as Lager makes them and the person writes them on the box: K0007 = NR 7.
public static class BoxCodes
{
    public static string For(int number) => $"K{number:D4}";

    public static int Number(string code) => int.TryParse(code.Trim().TrimStart('K', 'k'), out var n) ? n : 0;

    // The next code after the last one: K0001, K0002 ...
    public static string Next(string? last) => For(last is null ? 1 : Number(last) + 1);
}

// EAN / ISBN rules.
public static class Barcodes
{
    public static string Clean(string code) => new(code.Where(char.IsDigit).ToArray());

    // Books before 2007 carry a 10-digit ISBN (last digit may be X); Google Books and the sites want the 13-digit one.
    public static string ToIsbn13(string typed)
    {
        var raw = new string(typed.Where(c => char.IsDigit(c) || c is 'X' or 'x').ToArray()).ToUpperInvariant();
        if (raw.Length != 10) return Clean(raw);
        var core = "978" + raw[..9];
        var sum = core.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        return core + (10 - sum % 10) % 10;
    }

    public static bool IsIsbn(string code) => code.Length == 13 && (code.StartsWith("978") || code.StartsWith("979"));
}
