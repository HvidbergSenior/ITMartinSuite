using System.Text.RegularExpressions;

namespace ITMartinLager.Domain;

// The gold list (user 2026-10-12: "Most valued Anders And. Most valued Jumbobøger etc... Maybe even 50 in each category
// ... so that if one match it will say... GOLD"). Per category, the dearest single issues for sale right now, read
// off the sites' "dearest first" lists. Whole collections, lots and placeholder prices are left out - a gold entry is
// one issue (a number and/or a year), so a registered item can be matched against it.
// ByTitle (2026-10-20, user: "I want bronze, silver, gold on ever producttype"): books, films, music and Magic cards
// are one title, not a numbered issue - an entry is the listing's own title, and an item matches when its title's words
// are all in it.
public sealed record GoldCategory(string Kind, string Name, string Query, string[] MustHave, bool NeedsNumber, bool ByTitle = false)
{
    public string Type => ProductTypes.For(Kind);
}

public sealed class GoldEntry
{
    public int Id { get; set; }
    public string Category { get; set; } = "";   // GoldCategory.Name
    public string Kind { get; set; } = "";
    public string Number { get; set; } = "";
    public int? Year { get; set; }
    public string Name { get; set; } = "";       // the dearest listing's own title, so a wrong reading can be seen
    public int Kr { get; set; }                  // the middle of the listings for this issue (one dreamer is not gold)
    public int Listings { get; set; }
    public string Source { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime SeenAt { get; set; } = DateTime.UtcNow;

    public string Issue => string.Join(" ", new[] { Number != "" ? "nr. " + Number : "", Year?.ToString() ?? "" }.Where(s => s != ""));
    public string Tier => Gold.TierOf(Kr);
    /// <summary>"🥈 SØLV" - with a sign and a word, never colour alone.</summary>
    public string Badge => Gold.BadgeOf(Tier);
}

public static partial class Gold
{
    // Three tiers by price (user 2026-10-12: "what about a silver as well? Like not the highest price but valuable...
    // And the bronze. Interesting"). By kroner, not by rank in the category - the dearest Fantomet at 400 kr is silver,
    // not gold. Up to 50 per tier per category.
    public const int GoldFrom = 500, SilverFrom = 200, BronzeFrom = 100;
    public const string GoldTier = "Guld", SilverTier = "Sølv", BronzeTier = "Bronze";
    public const int PerCategory = 150;
    public const int PerTier = 50;

    public static string TierOf(int kr) => kr >= GoldFrom ? GoldTier : kr >= SilverFrom ? SilverTier : BronzeTier;
    public static string BadgeOf(string tier) => tier switch { GoldTier => "🏆 GULD", SilverTier => "🥈 SØLV", _ => "🥉 BRONZE" };

    public static readonly GoldCategory[] Categories =
    [
        new(Kinds.AndersAnd, "Anders And & Co.", "anders and & co", ["anders", "and"], false),
        new(Kinds.Jumbobog, "Jumbobøger", "jumbobog", ["jumbobog"], true),
        new(Kinds.Tegneserie, "Tintin", "tintin", ["tintin"], false),
        new(Kinds.Tegneserie, "Asterix", "asterix", ["asterix"], false),
        new(Kinds.Tegneserie, "Lucky Luke", "lucky luke", ["lucky", "luke"], false),
        new(Kinds.Tegneserie, "Fantomet", "fantomet", ["fantomet"], false),
        new(Kinds.Tegneserie, "Superserien", "superserien", ["superserien"], false),
        new(Kinds.Tegneserie, "Storm P.", "storm p", ["storm"], false),
        new(Kinds.Tegneserie, "Gyldenspjæt", "gyldenspjæt", ["gyldenspjæt"], false),
        // Superhelte - numbered issues, like the blade.
        new(Kinds.Superhelte, "X-Men", "x-men", ["men"], true),
        new(Kinds.Superhelte, "Spider-Man", "spider-man", ["spider"], true),
        new(Kinds.Superhelte, "Batman", "batman", ["batman"], true),
        new(Kinds.Superhelte, "Superman", "superman", ["superman"], true),
        new(Kinds.Superhelte, "Fantastic Four", "fantastic four", ["fantastic"], true),
        // One title each.
        new(Kinds.Bog, "Bøger – 1. udgave", "1. udgave", ["udgave"], false, ByTitle: true),
        new(Kinds.Bog, "Bøger – signeret", "signeret bog", ["signeret"], false, ByTitle: true),
        new(Kinds.Vinyl, "Vinyl", "vinyl lp", [], false, ByTitle: true),
        new(Kinds.Cd, "CD", "cd album", [], false, ByTitle: true),
        new(Kinds.Kassette, "Kassettebånd", "kassettebånd", [], false, ByTitle: true),
        new(Kinds.Vhs, "VHS", "vhs film", [], false, ByTitle: true),
        new(Kinds.Dvd, "DVD", "dvd film", [], false, ByTitle: true),
        new(Kinds.BluRay, "Blu-ray", "blu-ray", [], false, ByTitle: true),
        new(Kinds.Magic, "Magic-kort", "magic the gathering", [], false, ByTitle: true),
    ];

    /// <summary>The product types that have a gold list, in the order of the Guld page.</summary>
    public static string[] Types => ProductTypes.All.Where(t => Categories.Any(c => c.Type == t)).ToArray();

    // Words that make a listing many things, not one issue.
    private static readonly string[] Lots = ["samling", "samlinger", "komplet", "komplette", "stk", "pakke", "kasse", "lot", "bundle",
        "blandet", "blandede", "bind", "dele", "diverse", "flere", "mange", "partier", "parti", "samlemapper", "samlemappe", "årgange",
        // The 2026-10-13 list still had "1961, 48 numre", "155 blade", "52 hæfter i alt", "3 dtk." and bound years at the top.
        "numre", "blade", "hæfter", "dtk", "indbundet", "indbundne"];

    // A reprint is not the issue it copies: "Genoptryk af nr. 1 marts 1949" sold as gold for nr. 1 1949 (2026-10-13).
    private static readonly string[] Reprints = ["genoptryk", "genoptrykt", "nyoptryk", "faksimile", "reprint"];

    // Not a blad, album or book: the first gold list (2026-10-12) had an Omega "TinTin" watch, a 50 euro Asterix coin,
    // posters, Storm P porcelain figures and framed notice boards at the top.
    private static readonly string[] NotPaper = ["ur", "armbåndsur", "omega", "rolex", "mønt", "mønter", "euro", "proof", "plakat", "plakater",
        "poster", "figur", "figurer", "porcelæn", "porcelænsfigur", "statue", "kunsttryk", "litografi", "tryk", "serigrafi", "ramme",
        "indrammet", "opslagstavle", "lampe", "krus", "kop", "tallerken", "platte", "skilt", "emaljeskilt", "frimærke", "frimærker",
        "legetøj", "bamse", "puslespil", "spil", "tshirt", "t-shirt", "trøje", "kasket", "dvd", "vhs", "film", "lp", "vinyl", "cd",
        "bing", "grøndahl", "royal", "copenhagen", "maleri", "tegning", "original-tegning", "akvarel", "nøglering", "pin", "badge"];

    // The same for the other product types: the machine, not the record or the film; single cards, not sealed boxes.
    private static readonly Dictionary<string, string[]> NotThis = new()
    {
        [ProductTypes.Musik] = ["pladespiller", "forstærker", "højttaler", "højttalere", "receiver", "radio", "båndoptager", "walkman",
            "grammofon", "nål", "pickup", "plakat", "plakater", "poster", "tshirt", "t-shirt", "ramme", "indrammet", "dvd", "vhs", "bog"],
        [ProductTypes.Film] = ["afspiller", "player", "videomaskine", "maskine", "fjernbetjening", "plakat", "plakater", "poster", "figur",
            "figurer", "kamera", "fjernsyn", "bog", "vinyl"],
        [ProductTypes.Magic] = ["booster", "display", "sleeves", "playmat", "binder", "æske", "deck", "bundle", "starter"],
        [ProductTypes.Boeger] = ["dvd", "vhs", "vinyl", "plakat", "poster", "figur", "maleri", "tegning", "litografi", "kunsttryk"],
    };

    private static string[] NotFor(GoldCategory c) => NotThis.GetValueOrDefault(c.Type) ?? NotPaper;

    /// <summary>The dearest single issues (or titles) among the listings, grouped by issue (or title), top 50 per tier.</summary>
    public static List<GoldEntry> Pick(GoldCategory c, IEnumerable<Offer> offers, string source, DateTime now)
    {
        var single = new List<(string Number, int? Year, string Title, Offer Offer)>();
        var not = NotFor(c);
        foreach (var o in offers)
        {
            if (o.Price is not { } kr || kr <= 0 || ListingParse.Placeholder(kr)) continue;
            var words = ListingParse.Words(o.Name);
            if (!c.MustHave.All(m => words.Any(w => w.StartsWith(m)))) continue;
            if (words.Any(w => Lots.Contains(w) || not.Contains(w) || Reprints.Contains(w))
                || RangeRx().IsMatch(o.Name) || DecadeRx().IsMatch(o.Name)) continue;
            if (c.ByTitle)
            {
                // One title: the listing's own words are what an item is matched against.
                if (Item.TitleKey(o.Name).Length < 4) continue;
                single.Add(("", null, Item.TitleKey(o.Name), o));
                continue;
            }
            if (c.Kind == Kinds.AndersAnd && words.Contains("bog")) continue;   // "Anders And bog – En daglig dosis" is a book, not the blad
            var number = NumberRx().Match(o.Name) is { Success: true } n ? n.Groups[1].Value.TrimStart('0') : "";
            if (number == "" && words.Contains("årgang")) continue;   // "årgang 1986" with no number is the whole year
            int? year = YearRx().Matches(o.Name).Select(m => int.Parse(m.Value)).Distinct().ToList() is { Count: 1 } ys ? ys[0] : null;
            if (number == "" && (c.NeedsNumber || year is null)) continue;   // one issue must be named
            single.Add((number, year, "", o));
        }
        return single.GroupBy(x => (x.Number, x.Year, x.Title))
            .Select(g =>
            {
                var prices = g.Select(x => (int)x.Offer.Price!.Value).OrderBy(p => p).ToList();
                var top = g.MaxBy(x => x.Offer.Price)!.Offer;
                return new GoldEntry
                {
                    Category = c.Name, Kind = c.Kind, Number = g.Key.Number, Year = g.Key.Year, Name = top.Name,
                    Kr = prices[prices.Count / 2], Listings = prices.Count, Source = source, Url = top.Url, SeenAt = now,
                };
            })
            .Where(e => e.Kr >= BronzeFrom)
            .GroupBy(e => TierOf(e.Kr)).SelectMany(t => t.OrderByDescending(e => e.Kr).Take(PerTier))
            .OrderByDescending(e => e.Kr).ToList();
    }

    /// <summary>The one-line answer to "is this worth looking into?" (user 2026-10-12: "when I scan or look at one
    /// product i need to see if it is interesting to make further investigation or not"). Its own valued price first
    /// (when the hunter has one), then the exact issue on the list, then "the series has valuable issues - check".
    /// Null = ordinary, nothing to dig into.</summary>
    public static string? Interesting(IReadOnlyList<GoldEntry> list, string kind, string title, string series, string number, int? year, decimal? ownPrice = null)
    {
        if (ownPrice is { } p && p >= BronzeFrom) return $"{BadgeOf(TierOf((int)p))} – vurderet til ca. {p:0} kr";
        if (Match(list, kind, title, series, number, year) is { } e) return $"{e.Badge} – {e.Category} {e.Issue} sælges for ca. {e.Kr} kr";
        var words = ListingParse.Words($"{title} {series}");
        // "Måske" is for a numbered series with valuable issues; a single title is either on the list or not.
        var cat = Categories.FirstOrDefault(c => !c.ByTitle && c.Kind == kind && c.MustHave.All(m => words.Any(w => w.StartsWith(m))));
        if (cat is null) return null;
        if (year is not null && list.Where(e => e.Category == cat.Name && e.Number == "" && e.Year == year).MaxBy(e => e.Kr) is { } sameYear)
            return $"🔎 MÅSKE – tjek nærmere: et {cat.Name} fra {year} sælges for ca. {sameYear.Kr} kr. Se om det er dette nummer.";
        var valuable = list.Where(e => e.Category == cat.Name && e.Kr >= SilverFrom).ToList();
        if (valuable.Count == 0) return null;
        var years = valuable.Where(e => e.Year is not null).Select(e => e.Year!.Value).OrderBy(y => y).ToList();
        var span = years.Count == 0 ? "" : years[0] == years[^1] ? $" fra {years[0]}" : $" fra {years[0]}-{years[^1]}";
        return $"🔎 MÅSKE – tjek nærmere: {cat.Name} har {valuable.Count} numre{span} til 200 kr eller mere. Se om det er et af dem på Guld-listen.";
    }

    /// <summary>The mark as bidders on Live Auktion see it (2026-10-12: "put interesting and bronze, silver, gold on
    /// liveauctions"): the badge and what it goes for elsewhere - no internal words like "Guld-listen".</summary>
    public static string? ForAuction(IReadOnlyList<GoldEntry> list, string kind, string title, string series, string number, int? year, decimal? ownPrice = null)
    {
        if (ownPrice is { } p && p >= BronzeFrom) return $"{BadgeOf(TierOf((int)p))} – sælges for ca. {p:0} kr andre steder";
        if (Match(list, kind, title, series, number, year) is { } e) return $"{e.Badge} – sælges for ca. {e.Kr} kr andre steder";
        return Interesting(list, kind, title, series, number, year) is { } note && note.StartsWith("🔎")
            ? "🔎 MÅSKE – fra en serie med eftertragtede samlernumre" : null;
    }

    /// <summary>Order for "go through all interesting in one go": gold, silver, bronze, then "tjek nærmere".</summary>
    public static int Rank(string note) => note.StartsWith("🏆") ? 0 : note.StartsWith("🥈") ? 1 : note.StartsWith("🥉") ? 2 : 3;
    /// <summary>The four kinds of "interesting", in order, for buttons: Guld, Sølv, Bronze, Måske (user 2026-10-12).</summary>
    public static readonly string[] InterestingKinds = ["🏆 Guld", "🥈 Sølv", "🥉 Bronze", "🔎 Måske"];

    /// <summary>Is this thing on the gold list? Same kind, the category's words in its title or series, and the same
    /// issue: number when the entry has one, year when the entry has one.</summary>
    public static GoldEntry? Match(IReadOnlyList<GoldEntry> list, string kind, string title, string series, string number, int? year)
    {
        var words = ListingParse.Words($"{title} {series}");
        var num = number.Trim().TrimStart('0');
        return list.Where(e => e.Kind == kind && Categories.FirstOrDefault(c => c.Name == e.Category) is { } c
                && (c.ByTitle
                    ? TitleIn(e.Name, title)
                    : c.MustHave.All(m => words.Any(w => w.StartsWith(m)))
                      && (e.Number == "" || e.Number == num)
                      && (e.Year is null || e.Year == year)
                      && (e.Number != "" || e.Year is not null)
                      // A weekly blad has 52 issues a year: a listing with only the year is not this issue (2026-10-12).
                      && (e.Number != "" || kind != Kinds.AndersAnd)))
            .MaxBy(e => e.Kr);
    }

    // Every word of the item's title is in the listing's title. One short word ("Mort", "Heat") is too little to be sure.
    private static bool TitleIn(string listing, string title)
    {
        var want = ListingParse.Words(title);
        if (want.Count == 0 || (want.Count == 1 && want[0].Length < 6)) return false;
        var have = ListingParse.Words(listing).ToHashSet();
        return want.All(have.Contains);
    }

    [GeneratedRegex(@"(?:\bnr\.?|\bno\.?|#)\s*(\d{1,4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex NumberRx();
    [GeneratedRegex(@"\b(19[3-9]\d|20[0-2]\d)\b")]
    private static partial Regex YearRx();
    [GeneratedRegex(@"\b\d{1,4}\s*-\s*\d{1,4}\b")]
    private static partial Regex RangeRx();
    // "1960'erne", "1980’erne", "1950’ rene": a decade is many issues.
    [GeneratedRegex(@"\b(?:19|20)\d0\s*['’´]?\s*(?:erne|rne|rene)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DecadeRx();
}
