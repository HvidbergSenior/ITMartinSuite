namespace ITMartinLager.Domain;

// The price rules: which items are hunted, what is searched for, which finds are kept and what they add up to
// (2026-10-10, user: "as many places as possible ... facts are needed like 'found on X til 45 kr. Link'").
public static class Pricing
{
    // Everything but Magic cards, which get Cardmarket's price from the MTG Scanner (user 2026-10-12: "also cds, dvds etc").
    public static readonly string[] HuntedKinds = [Kinds.Bog, Kinds.Tegneserie, Kinds.Jumbobog, Kinds.AndersAnd, Kinds.Magasin,
        Kinds.Cd, Kinds.Dvd, Kinds.BluRay, Kinds.Konsolspil, Kinds.Merch, Kinds.Andet];
    public static readonly string[] PaperKinds = [Kinds.Bog, Kinds.Tegneserie, Kinds.Jumbobog, Kinds.AndersAnd, Kinds.Magasin];

    // The shop sells 10 % under the other sites (user 2026-10-12: "We sell 10 pct below other sites"), and shows the
    // customer the links that prove it.
    public const decimal ShopFactor = 0.9m;

    // Under 20 kr: whole kroner (a Magic card for 3 kr stays 3 kr). From 20 kr: down to the nearest 5, so it stays under.
    public static int? ShopPrice(decimal? market)
    {
        if (market is not { } m || m <= 0) return null;
        var p = m * ShopFactor;
        return p < 20 ? (int)Math.Max(1, Math.Round(p)) : (int)(Math.Floor(p / 5) * 5);
    }

    // Not searched again for this long; nothing answered = try again tomorrow.
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    private const int KeepPerSource = 12;

    public static string Isbn(string barcode) => Barcodes.IsIsbn(barcode) ? barcode : "";

    // Comics are sold by issue: "Anders And & Co." alone is a thousand different blade.
    public static string TitleFor(Item i)
    {
        var t = i.Title.Trim() != "" ? i.Title.Trim() : i.Series.Trim();
        return i.Number != "" && !ListingParse.Words(t).Contains(i.Number.Trim().ToLowerInvariant()) ? $"{t} {i.Number.Trim()}" : t;
    }

    // "surname title" - what most sites find best.
    public static string QueryFor(string title, string author)
    {
        var surname = author.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        return $"{surname} {title}".Trim();
    }

    // What the price search is made of - when it changes, the old prices are about another book.
    public static string SearchKey(Item i) => $"{i.Kind}|{i.Title}|{i.Artist}|{i.Series}|{i.Number}|{i.Barcode}";

    // The cheapest, the ones around the middle and the dearest - so both "normally 5 kr" and "50 kr on Amazon" are kept.
    public static List<Offer> Keep(Listings r)
    {
        var priced = r.All.Where(o => o.Price is > 0).DistinctBy(o => (o.Url, o.Price)).OrderBy(o => o.Price).ToList();
        if (priced.Count <= KeepPerSource) return priced;
        var mid = priced.Count / 2;
        return priced.Take(4).Concat(priced.Skip(mid - 2).Take(4)).Concat(priced.TakeLast(4)).Distinct().ToList();
    }

    // ".../Mort-Terry-Pratchett-Corgi-Books/31992988332/bd" -> "31992988332"
    public static string ListingNumber(string url) =>
        System.Text.RegularExpressions.Regex.Match(url, @"/(\d{6,})/b[di]") is { Success: true } m ? m.Groups[1].Value : "";

    // The normal used price: the middle of the used asking prices, after leaving out the ones over 3x the middle.
    public static int? NormalOf(IReadOnlyCollection<int> used)
    {
        if (used.Count == 0) return null;
        var sorted = used.OrderBy(x => x).ToList();
        var m = sorted[sorted.Count / 2];
        var sane = sorted.Where(x => x <= m * 3).ToList();
        return sane[sane.Count / 2];
    }

    // "10× normalprisen" - only said when it is far off, so the list does not shout about everything.
    public static string Flag(int kr, int? normal) =>
        normal is not { } n || n <= 0 ? ""
        : kr >= n * 3 ? $"{Math.Round((double)kr / n):0}× normalprisen"
        : kr * 3 <= n ? "under en tredjedel af normalprisen" : "";
}

// What one site said - the hunter's input. Null result = the site could not be asked.
public sealed record SourceAnswer(string Source, bool IsNew, Listings? Result);

// One hunt's outcome for one item: the per-site checks, the finds kept as proof and the normal used price.
public sealed record HuntResult(List<PriceCheck> Checks, List<PriceFind> Finds, int Answered, int? Normal)
{
    public static HuntResult From(int itemId, IReadOnlyList<SourceAnswer> answers, DateTime now)
    {
        // ZVAB is AbeBooks' German front - the same listing (same number) is shown once, under AbeBooks.
        var abeIds = answers.Where(a => a.Source == "AbeBooks" && a.Result is not null)
            .SelectMany(a => a.Result!.All).Select(o => Pricing.ListingNumber(o.Url)).Where(n => n != "").ToHashSet();
        IEnumerable<Offer> Kept(SourceAnswer a) =>
            Pricing.Keep(a.Result!).Where(o => a.Source != "ZVAB" || !abeIds.Contains(Pricing.ListingNumber(o.Url)));

        var checks = new List<PriceCheck>();
        var finds = new List<PriceFind>();
        foreach (var a in answers)
        {
            var r = a.Result;
            checks.Add(new PriceCheck
            {
                ItemId = itemId, Source = a.Source, IsNew = a.IsNew, Ok = r is not null, Count = r?.Count ?? 0,
                Low = Kr(r?.Low), Median = Kr(r?.Median), High = Kr(r?.High), Wild = Kr(r?.Wild), Ambiguous = r?.Ambiguous ?? false,
                SearchUrl = r?.Url ?? "", CheckedAt = now,
            });
            if (r is null || r.Ambiguous) continue;
            finds.AddRange(Kept(a).Select(o => new PriceFind
            {
                ItemId = itemId, Source = a.Source, IsNew = a.IsNew, Name = Cut(o.Name, 300), Kr = (int)o.Price!.Value,
                Original = o.Original, Url = o.Url, FoundAt = now,
            }));
        }
        var used = answers.Where(a => !a.IsNew && a.Result is { Ambiguous: false }).SelectMany(Kept).Select(o => (int)o.Price!.Value).ToList();
        return new HuntResult(checks, finds, answers.Count(a => a.Result is not null), Pricing.NormalOf(used));
    }

    // Onto the item: when it was hunted (nothing answered = try again tomorrow, not in 30 days) and the price hint.
    public void ApplyTo(Item item, DateTime now)
    {
        item.PricesAt = Answered == 0 ? now - Pricing.StaleAfter + TimeSpan.FromDays(1) : now;
        if (ShopPrice(Normal) is { } p) item.PriceHint = p;
    }

    private static int? ShopPrice(int? normal) => Pricing.ShopPrice(normal);
    private static int? Kr(decimal? v) => v is null ? null : (int)Math.Round(v.Value);
    private static string Cut(string s, int n) => s.Length <= n ? s : s[..n];
}
