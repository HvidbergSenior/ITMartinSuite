using System.Text.RegularExpressions;

namespace ITMartinLager.Domain;

// The gold list (user 2026-10-12: "Most valued Anders And. Most valued Jumbobøger etc... Maybe even 50 in each category
// ... so that if one match it will say... GOLD"). Per category, the dearest single issues for sale right now, read
// off the sites' "dearest first" lists. Whole collections, lots and placeholder prices are left out - a gold entry is
// one issue (a number and/or a year), so a registered item can be matched against it.
public sealed record GoldCategory(string Kind, string Name, string Query, string[] MustHave, bool NeedsNumber);

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
    ];

    // Words that make a listing many things, not one issue.
    private static readonly string[] Lots = ["samling", "samlinger", "komplet", "komplette", "stk", "pakke", "kasse", "lot", "bundle",
        "blandet", "blandede", "bind", "dele", "diverse", "flere", "mange", "partier", "parti", "samlemapper", "samlemappe", "årgange"];

    // Not a blad, album or book: the first gold list (2026-10-12) had an Omega "TinTin" watch, a 50 euro Asterix coin,
    // posters, Storm P porcelain figures and framed notice boards at the top.
    private static readonly string[] NotPaper = ["ur", "armbåndsur", "omega", "rolex", "mønt", "mønter", "euro", "proof", "plakat", "plakater",
        "poster", "figur", "figurer", "porcelæn", "porcelænsfigur", "statue", "kunsttryk", "litografi", "tryk", "serigrafi", "ramme",
        "indrammet", "opslagstavle", "lampe", "krus", "kop", "tallerken", "platte", "skilt", "emaljeskilt", "frimærke", "frimærker",
        "legetøj", "bamse", "puslespil", "spil", "tshirt", "t-shirt", "trøje", "kasket", "dvd", "vhs", "film", "lp", "vinyl", "cd",
        "bing", "grøndahl", "royal", "copenhagen", "maleri", "tegning", "original-tegning", "akvarel", "nøglering", "pin", "badge"];

    /// <summary>The dearest single issues among the listings, grouped by issue, top 50.</summary>
    public static List<GoldEntry> Pick(GoldCategory c, IEnumerable<Offer> offers, string source, DateTime now)
    {
        var single = new List<(string Number, int? Year, Offer Offer)>();
        foreach (var o in offers)
        {
            if (o.Price is not { } kr || kr <= 0 || ListingParse.Placeholder(kr)) continue;
            var words = ListingParse.Words(o.Name);
            if (!c.MustHave.All(m => words.Any(w => w.StartsWith(m)))) continue;
            if (words.Any(w => Lots.Contains(w) || NotPaper.Contains(w)) || RangeRx().IsMatch(o.Name)) continue;
            var number = NumberRx().Match(o.Name) is { Success: true } n ? n.Groups[1].Value.TrimStart('0') : "";
            int? year = YearRx().Matches(o.Name).Select(m => int.Parse(m.Value)).Distinct().ToList() is { Count: 1 } ys ? ys[0] : null;
            if (number == "" && (c.NeedsNumber || year is null)) continue;   // one issue must be named
            single.Add((number, year, o));
        }
        return single.GroupBy(x => (x.Number, x.Year))
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

    /// <summary>Is this thing on the gold list? Same kind, the category's words in its title or series, and the same
    /// issue: number when the entry has one, year when the entry has one.</summary>
    public static GoldEntry? Match(IReadOnlyList<GoldEntry> list, string kind, string title, string series, string number, int? year)
    {
        var words = ListingParse.Words($"{title} {series}");
        var num = number.Trim().TrimStart('0');
        return list.Where(e => e.Kind == kind
                && Categories.FirstOrDefault(c => c.Name == e.Category) is { } c && c.MustHave.All(m => words.Any(w => w.StartsWith(m)))
                && (e.Number == "" || e.Number == num)
                && (e.Year is null || e.Year == year)
                && (e.Number != "" || e.Year is not null))
            .MaxBy(e => e.Kr);
    }

    [GeneratedRegex(@"(?:\bnr\.?|\bno\.?|#)\s*(\d{1,4})\b", RegexOptions.IgnoreCase)]
    private static partial Regex NumberRx();
    [GeneratedRegex(@"\b(19[3-9]\d|20[0-2]\d)\b")]
    private static partial Regex YearRx();
    [GeneratedRegex(@"\b\d{1,4}\s*-\s*\d{1,4}\b")]
    private static partial Regex RangeRx();
}
