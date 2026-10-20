namespace ITMartinLager.Domain;

// The price rules: which items are hunted, what is searched for, which finds are kept and what they add up to
// (2026-10-10, user: "as many places as possible ... facts are needed like 'found on X til 45 kr. Link'").
public static partial class Pricing
{
    // Everything but Magic cards, which get Cardmarket's price from the MTG Scanner (user 2026-10-12: "also cds, dvds etc").
    public static readonly string[] HuntedKinds = [Kinds.Bog, Kinds.Tegneserie, Kinds.Superhelte, Kinds.Jumbobog, Kinds.AndersAnd, Kinds.Magasin,
        Kinds.Cd, Kinds.Vinyl, Kinds.Kassette, Kinds.Dvd, Kinds.BluRay, Kinds.Vhs, Kinds.Konsolspil, Kinds.Merch, Kinds.Andet];
    public static readonly string[] PaperKinds = [Kinds.Bog, Kinds.Tegneserie, Kinds.Superhelte, Kinds.Jumbobog, Kinds.AndersAnd, Kinds.Magasin];
    // The international antiquarian markets (AbeBooks, ZVAB, Bokbörsen) have books and albums, not Danish weekly blade
    // or Jumbobøger - asked anyway, they only answer with foreign books that share a word ("anders").
    public static readonly string[] AntiquarianKinds = [Kinds.Bog, Kinds.Tegneserie, Kinds.Magasin];

    // Pegasus (pegasus.dk, the comic shop in Copenhagen) blocks automatic searches with a browser check, and its search
    // is a form, not a link. So comics get a link to the shop and the words to search for, and a person looks (user 2026-10-13).
    public const string PegasusUrl = "https://www.pegasus.dk";
    public static bool PegasusFits(Item i) => i.Kind is Kinds.Tegneserie or Kinds.Superhelte or Kinds.Jumbobog or Kinds.AndersAnd;

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

    // Comics are sold by issue: "Anders And & Co." alone is a thousand different blade. A weekly blad is only one issue
    // with its YEAR too - nr. 12 is in every year (2026-10-12 test: "Anders And & Co. 12" from 1968 matched 2021's nr. 12),
    // so the year joins the search and, being a number, must be in every listing that counts.
    public static string TitleFor(Item i)
    {
        var t = i.Title.Trim() != "" ? i.Title.Trim() : i.Series.Trim();
        var words = ListingParse.Words(t);
        if (i.Number != "" && !words.Contains(i.Number.Trim().ToLowerInvariant())) t = $"{t} {i.Number.Trim()}";
        if (i.Kind is Kinds.AndersAnd or Kinds.Magasin && i.Year is { } y && !words.Contains(y.ToString())) t = $"{t} {y}";
        return t;
    }

    // Albums and annuals with both a number and a year ("Storm Petersen Album", 29. årgang, 1941): sellers write the year,
    // seldom the number - so when nothing matched, the hunter asks once more with the year instead (2026-10-12).
    public static string? AlternativeTitleFor(Item i)
    {
        if (i.Number.Trim() == "" || i.Year is not { } y || i.Kind == Kinds.AndersAnd) return null;
        var t = i.Title.Trim() != "" ? i.Title.Trim() : i.Series.Trim();
        return ListingParse.Words(t).Contains(y.ToString()) ? t : $"{t} {y}";
    }

    // Nothing found for the title (2026-10-13: "Vampyrernes nat 5", "Walt Disney's Anders And & Co. 3 1955" and long
    // "Spidey Saves the Day! The ..." titles got 0 hits on every site): sellers write the series and the number
    // ("Sammy nr. 5", "Anders And 1955 nr 3") and short titles. Broader searches, tried in this order until one answers.
    public static List<string> FallbackTitlesFor(Item i)
    {
        var title = i.Title.Trim();
        var series = i.Series.Trim();
        var number = i.Number.Trim();
        var year = i.Kind is Kinds.AndersAnd or Kinds.Magasin && i.Year is { } y ? y.ToString() : "";
        var tries = new List<string>();
        if (series != "")
        {
            if (number != "") tries.Add(string.Join(' ', new[] { series, number, year }.Where(s => s != "")));
            if (title != "" && !ListingParse.Words(title).Contains(ListingParse.Words(series).FirstOrDefault() ?? ""))
                tries.Add($"{series} {title}");
        }
        // The short title - but "The Amazing Spider-Man: Spidey Saves the Day!" is the series before the colon, and the
        // series alone finds every comic in it (111 Spider-Man listings): then the subtitle is the short title.
        var rest = title;
        var colon = title.IndexOf(':');
        if (colon > 0 && series != "" && string.Equals(title[..colon].Trim(), series, StringComparison.OrdinalIgnoreCase))
            rest = title[(colon + 1)..].Trim();
        var cut = rest.IndexOfAny([':', '!', '(', '–']);
        var shortTitle = (cut > 3 ? rest[..cut] : rest).Trim();
        if (shortTitle != title && shortTitle.Any(char.IsLetter) && !string.Equals(shortTitle, series, StringComparison.OrdinalIgnoreCase))
            tries.Add(number != "" ? $"{shortTitle} {number}" : shortTitle);
        var done = new HashSet<string?>([TitleFor(i), AlternativeTitleFor(i)], StringComparer.OrdinalIgnoreCase);
        return tries.Where(t => done.Add(t)).ToList();
    }

    // "Magen til" (user 2026-10-12: "Lige præcis denne findes ingen priser, men denne koster ..."): the same title
    // without the issue number and year - other årgange of the album, other issues of the blad. An Anders And blad keeps
    // its year (other issues from 1968). Null when that is no broader than the exact search.
    public static string? SimilarTitleFor(Item i)
    {
        var t = i.Title.Trim() != "" ? i.Title.Trim() : i.Series.Trim();
        var year = i.Year?.ToString();
        var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => new string(w.Where(char.IsLetterOrDigit).ToArray()) is var k && (k == "" || !ListingParse.IsNumber(k.ToLowerInvariant()))).ToList();
        if (!words.Any(w => w.Any(char.IsLetter))) return null;
        var broad = string.Join(' ', words);
        if (i.Kind == Kinds.AndersAnd && year is not null) broad = $"{broad} {year}";
        return broad == TitleFor(i) || broad == AlternativeTitleFor(i) ? null : broad;
    }

    // Is this listing really one ordinary copy of this item? (2026-10-12, Spider-Geddon #3: the sites' broad searches gave
    // "Edge of Spider-Geddon #3", "Spider-Girls #1-3 set", "#1 2 3 4 5 Run Set Lot", CGC 9.8 graded copies, 1:25 variant
    // covers and postcards - none of them the comic on the table.)
    private static readonly HashSet<string> NotOneCopy = ["set", "sets", "lot", "lots", "run", "complete", "komplett", "komplettsatz",
        "sammlung", "collection", "bundle", "samling", "komplet", "cgc", "cbcs", "pgx", "graded", "variant", "variante", "incentive",
        "postcard", "postkarte", "ansichtskarte", "postkort", "poster", "books", "bücher", "issues", "hefte"];

    // Brand, publisher and creator words a seller may put before a comic's title.
    private static readonly HashSet<string> Brands = ["marvel", "comics", "comic", "walt", "disney", "disneys", "tegneserie", "tegneserier",
        "album", "hergé", "herge", "image", "dark", "horse", "panini", "carlsen", "egmont", "serieforlaget", "interpresse", "semic", "the", "new"];

    public static bool IsOneCopyOf(Item item, Offer o)
    {
        var words = ListingParse.Words(o.Name);
        if (words.Any(NotOneCopy.Contains) || CopyRangeRx().IsMatch(o.Name) || RatioRx().IsMatch(o.Name)) return false;
        // A comic with an issue number: the title must come first (after brand/creator words) - "Edge of X" is another series.
        if (item.Kind is Kinds.Tegneserie or Kinds.Superhelte && item.Number.Trim() != "")
        {
            var title = ListingParse.Words(item.Title.Trim() != "" ? item.Title : item.Series).Where(w => !ListingParse.IsNumber(w)).ToList();
            var creator = ListingParse.Words(item.Artist).ToHashSet();
            var lead = words.SkipWhile(w => Brands.Contains(w) || creator.Contains(w)).ToList();
            // ... or the series first: "Sammy 5 Vampyrernes nat" (2026-10-13).
            var series = ListingParse.Words(item.Series).Where(w => !ListingParse.IsNumber(w)).FirstOrDefault();
            if (title.Count > 0 && (lead.Count == 0 || (lead[0] != title[0] && lead[0] != series))) return false;
        }
        return true;
    }

    /// <summary>The site's answer with only the listings that are one ordinary copy of this item.</summary>
    public static Listings OnlyThisItem(Item item, Listings r)
    {
        if (r.Ambiguous) return r;
        var kept = r.All.Where(o => IsOneCopyOf(item, o)).ToList();
        return kept.Count == r.All.Count ? r : ListingParse.Summarise(kept, r.Url);
    }

    // "1-5", "#1-3", "1 til 52", "#1,2,3," - several issues in one listing.
    [System.Text.RegularExpressions.GeneratedRegex(@"(?:#|\bnr\.?\s*|\bno\.?\s*)?\d{1,4}\s*(?:-|–|til|to)\s*#?\d{1,4}\b|#?\d{1,4}\s*,\s*#?\d{1,4}\s*,", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex CopyRangeRx();
    // "1:25", "1:50" - a ratio variant cover.
    [System.Text.RegularExpressions.GeneratedRegex(@"\b1\s*:\s*\d{2,4}\b")]
    private static partial System.Text.RegularExpressions.Regex RatioRx();

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

    // Danish market first (user 2026-10-12: "always prioritize Danish market ... I dont know if it can pay off to put
    // things on foreign markets"): our price comes from these when they have at least DanishEnough prices.
    public static readonly string[] DanishSources = ["DBA", "GulogGratis", "Nemos Bibliotek", "Faraos"];
    public const int DanishEnough = 2;

    /// <summary>The normal used price with Danish listings first: Danish only when there are enough, else everything.</summary>
    public static int? NormalDanishFirst(IReadOnlyCollection<(string Source, int Kr)> used)
    {
        var danish = used.Where(u => DanishSources.Contains(u.Source)).Select(u => u.Kr).ToList();
        return danish.Count >= DanishEnough ? NormalOf(danish) : NormalOf(used.Select(u => u.Kr).ToList());
    }

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
public sealed record HuntResult(List<PriceCheck> Checks, List<PriceFind> Finds, int Answered, int? Normal, int? SimilarNormal = null, int? NewNormal = null)
{
    public static HuntResult From(int itemId, IReadOnlyList<SourceAnswer> answers, DateTime now, IReadOnlyList<SourceAnswer>? similar = null)
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
        var used = answers.Where(a => !a.IsNew && a.Result is { Ambiguous: false })
            .SelectMany(a => Kept(a).Select(o => (a.Source, Kr: (int)o.Price!.Value))).ToList();

        // "Magen til": kept apart, flagged, and never part of the normal price.
        var like = (similar ?? []).Where(a => !a.IsNew && a.Result is { Ambiguous: false }).ToList();
        foreach (var a in like)
            finds.AddRange(Pricing.Keep(a.Result!).Select(o => new PriceFind
            {
                ItemId = itemId, Source = a.Source, Name = Cut(o.Name, 300), Kr = (int)o.Price!.Value, Original = o.Original,
                Url = o.Url, FoundAt = now, Similar = true,
            }));
        var likeKr = like.SelectMany(a => Pricing.Keep(a.Result!)).Select(o => (int)o.Price!.Value).ToList();
        // New shops (Saxo, Amazon ...): the price a new copy is compared with.
        var fresh = answers.Where(a => a.IsNew && a.Result is { Ambiguous: false }).SelectMany(a => Kept(a).Select(o => (int)o.Price!.Value)).ToList();
        return new HuntResult(checks, finds, answers.Count(a => a.Result is not null), Pricing.NormalDanishFirst(used), Pricing.NormalOf(likeKr),
            Pricing.NormalOf(fresh));
    }

    // Onto the item: when it was hunted (nothing answered = try again tomorrow, not in 30 days) and the price hint. A new
    // copy is priced 10 % under the new shops (2026-10-20); used prices only when no new shop has it.
    public void ApplyTo(Item item, DateTime now)
    {
        item.PricesAt = Answered == 0 ? now - Pricing.StaleAfter + TimeSpan.FromDays(1) : now;
        var market = item.Usage == Usages.New ? NewNormal ?? Normal : Normal;
        if (ShopPrice(market) is { } p) item.PriceHint = p;
        item.SimilarHint = Normal is null ? SimilarNormal : null;
    }

    private static int? ShopPrice(int? normal) => Pricing.ShopPrice(normal);
    private static int? Kr(decimal? v) => v is null ? null : (int)Math.Round(v.Value);
    private static string Cut(string s, int n) => s.Length <= n ? s : s[..n];
}
