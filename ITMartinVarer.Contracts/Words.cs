namespace ITMartinVarer.Contracts;

// The words every app uses about a thing. Stored as text everywhere, so a new kind never breaks old rows.

// What kind of thing it is.
public static class Kinds
{
    public const string Cd = "CD", Dvd = "DVD", BluRay = "Blu-ray", Magasin = "Magasin", AndersAnd = "Anders And-blad",
        Jumbobog = "Jumbobog", Tegneserie = "Tegneserie", Konsolspil = "Konsolspil", Bog = "Bog",
        Magic = "Magic-kort", Merch = "Merchandise", Andet = "Andet",
        // 2026-10-20 with the product-type services: film on tape, records and tapes, American superhero comics.
        Vhs = "VHS", Vinyl = "Vinyl", Kassette = "Kassettebånd", Superhelte = "Superhelteblad",
        // 2026-11-03 with the main categories: "Brætspil/kortspil".
        Braetspil = "Brætspil", Kortspil = "Kortspil";

    public static readonly string[] All = [AndersAnd, Jumbobog, Tegneserie, Superhelte, Magasin, Bog, Cd, Vinyl, Kassette, Dvd, BluRay, Vhs,
        Konsolspil, Braetspil, Kortspil, Magic, Merch, Andet];

    public static string Icon(string kind) => kind switch
    {
        Cd or Vinyl => "🎵", Kassette or Vhs => "📼", Dvd or BluRay => "💿", Magasin => "📰", AndersAnd or Jumbobog => "🦆",
        Tegneserie => "💥", Superhelte => "🦸",
        Konsolspil => "🎮", Braetspil or Kortspil => "🎲", Bog => "📚", Magic => "🃏", Merch => "🧸", _ => "📦",
    };

    public static string Normalise(string? kind) =>
        All.FirstOrDefault(k => string.Equals(k, kind?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Andet;

    /// <summary>Series the reader files as Tegneserie (an album) that are something else (user 2026-11-02: "anders and og co
    /// is under tegneserier", "Walt disney must Anders ANd", "Mammutbog are jumbobooks", "jonah hex is et blad", "Kong kylie
    /// is a blad"). A blad that is not Anders And is filed with the superhero blade = the Serieblade category.</summary>
    public static readonly IReadOnlyDictionary<string, string> SeriesKind = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Anders And & Co."] = AndersAnd, ["Anders And Ekstra"] = AndersAnd, ["Walt Disney's"] = AndersAnd,
        ["Walt Disney's månedshæfte"] = AndersAnd,
        ["Mammutbog"] = Jumbobog,
        ["Jonah Hex"] = Superhelte, ["Kong Kylie"] = Superhelte, ["Basserne"] = Superhelte, ["Skipper Skræk"] = Superhelte,
        ["Supermule"] = Superhelte,
    };

    /// <summary>Normalise, and a Tegneserie of a series in SeriesKind gets that kind.</summary>
    public static string Normalise(string? kind, string? series)
    {
        var k = Normalise(kind);
        return k == Tegneserie && series?.Trim() is { Length: > 0 } s && SeriesKind.TryGetValue(s, out var other) ? other : k;
    }

    /// <summary>The kind as a customer reads it: a Superhelteblad is shown as "Blad" (it also holds Jonah Hex and co.).</summary>
    public static string Label(string kind) => kind == Superhelte ? "Blad" : kind;
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

    // Personal test boxes (user 2026-10-25: "i want all helpers to have their own Testbox... Also myself"): the top
    // numbers, one per person, handed out from 9999 down. Never written on a real box; their things are only for show.
    public const int FirstTest = 9000;
    public static bool IsTest(string code) => Number(code) >= FirstTest;

    /// <summary>The box number in a scanned label QR ("https://lager.bogshoppen.dk/kasse/K0007" or just "K0007"); 0 = not a box label.</summary>
    public static int FromLabel(string? scanned)
    {
        var m = System.Text.RegularExpressions.Regex.Match(scanned ?? "", @"(?:^|/)[Kk](\d{1,4})/?$");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    /// <summary>The boxes from..to for a label sheet - at most <see cref="MaxSheet"/>, in order, never below 1.</summary>
    public const int MaxSheet = 60;
    public static List<string> Range(int from, int to)
    {
        from = Math.Max(1, from);
        to = Math.Clamp(to, from, from + MaxSheet - 1);
        return Enumerable.Range(from, to - from + 1).Select(For).ToList();
    }
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

// The product-type services (user 2026-10-20: "separate services for each producttype and the lager and vareinfo should
// be the shared base"): which one looks after a kind - identification, catalogue fields, price sources, gold lists.
// Lager decides the kind when it reads a photo, and this decides where it belongs.
/// <summary>Film on bogshoppen.dk is DVD, Blu-ray and Sæsoner (user 2026-11-02: "film should be DVD, Blueray and Seasons"):
/// a season box carries SeasonWord in its search text (Lager's Films.IsSeason), and the Sæsoner button searches it.</summary>
public static class FilmShelves
{
    public const string SeasonWord = "sæsoner";
}

public static class ProductTypes
{
    // The main categories (user 2026-11-03: "When searching and or doing things they should be split up in the main categories.
    // So that user can follow 1 or more types of products"). Tegneserier = albums (Lucky Luke); Serieblade = Danish blade (Anders
    // And, Basserne, Jonah Hex, Kong Kylie); Superhelte = Project X, Edderkoppen; Gamle blade = every blad before 1990.
    public const string Boeger = "Bøger", Tegneserier = "Tegneserier", Serieblade = "Serieblade", GamleBlade = "Gamle blade",
        Jumbo = "Jumbobøger", Superhelte = "Superhelte", Film = "Film", Musik = "Musik", Computerspil = "Computerspil",
        Braetspil = "Brætspil og kortspil", Magic = "Magic", Andet = "Andet";

    public static readonly string[] All = [Boeger, Tegneserier, Serieblade, GamleBlade, Jumbo, Superhelte, Film, Musik, Computerspil,
        Braetspil, Magic, Andet];

    /// <summary>A blad from before this year is a Gamle blade (user 2026-11-03: "before 1990 all blade").</summary>
    public const int OldBefore = 1990;

    /// <summary>Who looks after a category (user 2026-11-03: Rico has Tegneserier, Superhelte and Film; Martin the rest).</summary>
    public static string Responsible(string type) => type is Tegneserier or Superhelte or Film ? "Rico" : "Martin";

    /// <summary>Blade that are filed with the superhero blade (Kinds.Superhelte) but are Danish serieblade.</summary>
    public static readonly IReadOnlySet<string> BladSeries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Jonah Hex", "Kong Kylie", "Basserne", "Skipper Skræk", "Supermule", "Fantomet" };

    public static bool IsBlad(string kind) => kind is Kinds.AndersAnd or Kinds.Superhelte;

    /// <summary>The main category of a thing - from its kind, and for blade also its series and year.</summary>
    public static string For(string kind, string? series = null, int? year = null) => kind switch
    {
        _ when IsBlad(kind) && year is > 0 and < OldBefore => GamleBlade,
        Kinds.AndersAnd => Serieblade,
        Kinds.Superhelte => series?.Trim() is { Length: > 0 } s && BladSeries.Contains(s) ? Serieblade : Superhelte,
        Kinds.Magic => Magic,
        Kinds.Dvd or Kinds.BluRay or Kinds.Vhs => Film,
        Kinds.Cd or Kinds.Vinyl or Kinds.Kassette => Musik,
        Kinds.Tegneserie => Tegneserier,
        Kinds.Jumbobog => Jumbo,
        Kinds.Bog => Boeger,
        Kinds.Konsolspil => Computerspil,
        Kinds.Braetspil or Kinds.Kortspil => Braetspil,
        _ => Andet,   // merchandise, magazines, the rest
    };

    /// <summary>The kinds that can be in a category (a blad kind is in Serieblade, Gamle blade and Superhelte).</summary>
    public static string[] KindsOf(string type) => type switch
    {
        GamleBlade or Serieblade => [Kinds.AndersAnd, Kinds.Superhelte],
        Superhelte => [Kinds.Superhelte],
        _ => Kinds.All.Where(k => For(k) == type).ToArray(),
    };
}

// What a customer sees on bogshoppen.dk (user 2026-11-03: "the search engine is too complex" -> one search field and five
// categories): each shelf holds one or more main categories. Andet has no shelf (it is only found by the search).
public static class ShopShelves
{
    public const string Boeger = "Bøger", Tegneserier = "Tegneserier og blade", Film = "Film", Musik = "Musik", Spil = "Spil";

    public static readonly (string Name, string[] Types)[] All =
    [
        (Boeger, [ProductTypes.Boeger]),
        (Tegneserier, [ProductTypes.Tegneserier, ProductTypes.Serieblade, ProductTypes.GamleBlade, ProductTypes.Jumbo, ProductTypes.Superhelte]),
        (Film, [ProductTypes.Film]),
        (Musik, [ProductTypes.Musik]),
        (Spil, [ProductTypes.Computerspil, ProductTypes.Braetspil, ProductTypes.Magic]),
    ];

    public static bool IsShelf(string? name) => All.Any(s => s.Name == name);

    /// <summary>The shelf a main category is on; "" = none (Andet).</summary>
    public static string For(string type) => All.FirstOrDefault(s => s.Types.Contains(type)).Name ?? "";

    /// <summary>Where a shelf stands on the page (unknown last).</summary>
    public static int Order(string shelf) => Array.FindIndex(All, s => s.Name == shelf) is >= 0 and var i ? i : All.Length;

    /// <summary>The main categories on these shelves.</summary>
    public static List<string> TypesOf(IEnumerable<string> shelves) =>
        All.Where(s => shelves.Contains(s.Name)).SelectMany(s => s.Types).ToList();
}

// Which language a thing is in (user 2026-11-03: "all cats should have an English"). Read with the pile (no extra AI call);
// older things get Guess from the title.
public static class Languages
{
    public const string Dansk = "Dansk", Engelsk = "Engelsk", Andet = "Andet sprog";
    public static readonly string[] All = [Dansk, Engelsk, Andet];

    public static string Normalise(string? l) => l?.Trim().ToLowerInvariant() switch
    {
        "dansk" or "danish" or "da" => Dansk,
        "engelsk" or "english" or "en" => Engelsk,
        null or "" => "",
        _ => Andet,
    };

    private static readonly HashSet<string> EnglishWords = ["the", "of", "and", "to", "in", "with", "for", "from", "is", "a", "an",
        "my", "your", "how", "what", "who", "complete", "season", "guide", "book", "life", "love", "world", "story", "man", "woman"];
    private static readonly HashSet<string> DanishWords = ["og", "af", "en", "et", "det", "den", "til", "med", "på", "fra", "som", "er",
        "jeg", "du", "der", "ikke", "de", "for", "bog", "bogen", "historie", "historier", "sæson", "nr"];

    /// <summary>A guess from the words in the title (and series): English words and no Danish ones = Engelsk, else Dansk.
    /// Only for things read before the reader said the language.</summary>
    public static string Guess(string title, string series = "")
    {
        // "Anders And" is Danish, not the English "and".
        var text = $"{title} {series}".ToLowerInvariant().Replace("anders and", " ");
        if (text.Any(c => c is 'æ' or 'ø' or 'å')) return Dansk;
        var words = System.Text.RegularExpressions.Regex.Matches(text, @"[a-z]+").Select(m => m.Value).ToList();
        var en = words.Count(EnglishWords.Contains);
        var da = words.Count(w => DanishWords.Contains(w) && !EnglishWords.Contains(w));
        return en > 0 && da == 0 ? Engelsk : Dansk;
    }
}

// New or used (user 2026-10-20: "there should be a used and new field"). Not the same as the condition ("Som ny" is a
// used thing in fine shape): new = never sold before, e.g. still in its plastic. It decides which prices it is compared
// with, which offers cover it, and - for the bookkeeping - brugtmoms (used) or ordinary moms (new).
public static class Usages
{
    public const string Used = "Brugt", New = "Ny";
    public static readonly string[] All = [Used, New];

    /// <summary>Anything unknown is used - almost everything in the boxes is.</summary>
    public static string Normalise(string? u) => string.Equals(u?.Trim(), New, StringComparison.OrdinalIgnoreCase) ? New : Used;
}
