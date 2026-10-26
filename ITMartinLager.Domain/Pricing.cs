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

    /// <summary>Under this the AI was unsure what the thing is - no suggested price until a person has looked (2026-10-21).</summary>
    public const double MinConfidence = 0.6;

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
    // "The Face of Defeat..." (2026-10-25): an AI reading cut short - the dots are no part of the title and no site has them.
    // "Co." keeps its one dot.
    public static string CleanTitle(string title)
    {
        var t = title.Trim();
        return t.EndsWith("...") || t.EndsWith('…') ? t.TrimEnd('.', '…', ' ').Trim() : t;
    }

    // An Anders And blad with a number is sold as "Anders And & Co. 41 1987" - the headline on the cover ("Sluk lyset, hvis
    // du tør!") is in no listing, and with it 2026-10-26 found nothing on any site. The series is searched instead.
    private static string BaseTitle(Item i) =>
        i.Kind == Kinds.AndersAnd && i.Number.Trim() != "" && i.Series.Trim() != "" ? i.Series.Trim()
        : CleanTitle(i.Title) != "" ? CleanTitle(i.Title) : i.Series.Trim();

    public static string TitleFor(Item i)
    {
        var t = BaseTitle(i);
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
        var t = CleanTitle(i.Title) != "" ? CleanTitle(i.Title) : i.Series.Trim();
        return ListingParse.Words(t).Contains(y.ToString()) ? t : $"{t} {y}";
    }

    // Nothing found for the title (2026-10-13: "Vampyrernes nat 5", "Walt Disney's Anders And & Co. 3 1955" and long
    // "Spidey Saves the Day! The ..." titles got 0 hits on every site): sellers write the series and the number
    // ("Sammy nr. 5", "Anders And 1955 nr 3") and short titles. Broader searches, tried in this order until one answers.
    public static List<string> FallbackTitlesFor(Item i)
    {
        var title = CleanTitle(i.Title);
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
        var t = BaseTitle(i);
        var year = i.Year?.ToString();
        var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => new string(w.Where(char.IsLetterOrDigit).ToArray()) is var k && (k == "" || !ListingParse.IsNumber(k.ToLowerInvariant()))).ToList();
        if (!words.Any(w => w.Any(char.IsLetter))) return null;
        var broad = string.Join(' ', words);
        if (i.Kind == Kinds.AndersAnd && year is not null) broad = $"{broad} {year}";
        return broad == TitleFor(i) || broad == AlternativeTitleFor(i) ? null : broad;
    }

    // The series itself as a "magen til" search (2026-10-26, Basserne 612/423/823: no copy for sale, other issues at
    // 13-32 kr on Nemos): a comic or magazine with a series and a number - not Anders And, whose series is searched already.
    public static Item? SeriesItemFor(Item i)
    {
        var series = i.Series.Trim();
        if (i.Kind is not (Kinds.Tegneserie or Kinds.Superhelte or Kinds.Magasin) || series == "" || i.Number.Trim() == "") return null;
        return new Item { Kind = i.Kind, Title = series, Series = series, Artist = i.Artist, Note = i.Note };
    }

    // A reading the AI was unsure of (Basserne 779, confidence 0.4) is right after all when a used listing names the same
    // series and the same number - then it gets its price like any other.
    public static bool ConfirmsReading(Item i, string listing)
    {
        var number = i.Number.Trim().ToLowerInvariant();
        if (number == "") return false;
        var words = ListingParse.Words(listing);
        var name = ListingParse.Words(i.Series.Trim() != "" ? i.Series : i.Title).Where(w => !ListingParse.IsNumber(w)).ToList();
        return name.Count > 0 && name.All(words.Contains) && words.Contains(number)
               && (i.Year is not { } y || words.Contains(y.ToString()));
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

    // Another format of the same title (2026-10-21: the Californication CD was priced from LPs on DBA, 170 kr, while used
    // CDs go for 5-30 kr). Per kind: the words that mean the listing is NOT this format. Signed and box/deluxe copies
    // are other things too.
    private static readonly HashSet<string> NotTheRecord = ["signed", "signeret", "signiert", "autograph", "autographed", "box", "boxset",
        "deluxe", "slipmat", "shirt", "plakat", "poster"];
    private static readonly Dictionary<string, HashSet<string>> OtherFormats = new()
    {
        [Kinds.Cd] = ["vinyl", "vinil", "schallplatte", "kassette", "kassettebånd", "cassette", "tape", "dvd", "blu", "bluray", "vhs"],
        [Kinds.Vinyl] = ["kassette", "kassettebånd", "cassette", "tape", "dvd", "blu", "bluray", "vhs"],
        [Kinds.Kassette] = ["vinyl", "vinil", "schallplatte", "dvd", "blu", "bluray", "vhs"],
        [Kinds.Dvd] = ["blu", "bluray", "blueray", "vhs", "uhd", "steelbook", "vinyl", "soundtrack", "3d", "dvds", "boks", "samleboks"],
        [Kinds.BluRay] = ["vhs", "vinyl", "soundtrack"],
        // A comic of the same name is not the TV series, the film or the novel (2026-10-25: "The Walking Dead").
        [Kinds.Tegneserie] = ["dvd", "blu", "bluray", "vhs", "season", "sæson", "sesong", "staffel", "säsong", "kausi", "pokkari", "roman", "novel"],
        [Kinds.Superhelte] = ["dvd", "blu", "bluray", "vhs", "season", "sæson", "sesong", "staffel", "säsong", "kausi", "pokkari", "roman", "novel"],
        [Kinds.Vhs] = ["dvd", "blu", "bluray", "uhd", "vinyl", "soundtrack"],
    };

    /// <summary>The listing is of another format than the item (a CD item and an LP listing, a DVD and a Blu-ray ...).</summary>
    public static bool OtherFormat(string kind, string listing)
    {
        if (!OtherFormats.TryGetValue(kind, out var other)) return false;
        if (ListingParse.Words(listing).Any(w => other.Contains(w) || NotTheRecord.Contains(w))) return true;
        // Short words Words() leaves out: "LP", "CD", "4K" and 7"/12" singles.
        var low = " " + listing.ToLowerInvariant() + " ";
        if (kind != Kinds.Vinyl && (LpRx().IsMatch(low) || SingleRx().IsMatch(low))) return true;
        if (kind is Kinds.Vinyl or Kinds.Kassette && CdRx().IsMatch(low)) return true;
        if (kind is Kinds.Dvd or Kinds.Vhs && FourKRx().IsMatch(low)) return true;
        return false;
    }

    // Collector copies (2026-10-21: Birger og Viola 3 priced 220 kr from two "1. udgave, 1. oplag, Near Mint" listings):
    // a first printing, a graded or a special copy is a collector price, not what an ordinary copy sells for - unless
    // ours is one too.
    [System.Text.RegularExpressions.GeneratedRegex(@"\b1\.?\s*(udgave|udg\.?|oplag)\b|første\s*udgave|førsteudgave|first\s+edition|1st\s+edition|erstausgabe|first\s+printing", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex FirstEditionRx();
    [System.Text.RegularExpressions.GeneratedRegex(@"near\s*mint|very\s*fine|\bnm\+?(?=\W|$)|\bvf(?=\W|$)|\bmint\b|\bungelesen\b|\bklassiker\b|\blimited\b|\blimiteret\b|samlerudgave|collector", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex CollectorRx();

    /// <summary>A first edition or a graded/special copy, when the item itself is not one.</summary>
    public static bool CollectorCopy(Item item, string listing)
    {
        var ours = $"{item.Title} {item.Series} {item.Edition} {item.EditionHint}";
        if (FirstEditionRx().IsMatch(listing) && !FirstEditionRx().IsMatch(ours)) return true;
        return CollectorRx().IsMatch(listing) && !CollectorRx().IsMatch(ours);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"[\s(\[/,+-]lps?[\s)\]/,.:+-]", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex LpRx();
    [System.Text.RegularExpressions.GeneratedRegex("\\b(7|10|12)\\s*(\"|''|inch|tommer|zoll)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex SingleRx();
    [System.Text.RegularExpressions.GeneratedRegex(@"[\s(\[/,+-]cds?[\s)\]/,.:+-]", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex CdRx();
    [System.Text.RegularExpressions.GeneratedRegex(@"[\s(\[/,+-]4k[\s)\]/,.:+-]", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex FourKRx();

    // Sources that only ever answer in the item's own format (a CD search on Accord/Discogs is CDs).
    public static readonly HashSet<string> FormatSure = ["Discogs", "Accord"];

    private static readonly HashSet<string> FilmWords = ["dvd", "blu", "bluray", "blueray", "film", "movie", "vhs", "disc", "uhd", "dvdfilm"];

    /// <summary>A film listing must say it is a film (2026-10-25, Guardians of the Galaxy DVD: 38 links were LEGO, plush,
    /// costumes, the soundtrack CD and the comics): a format word, and no CD or comic number ("#19").</summary>
    // A book or a shirt of the same name is never a record or a film ("Love Sick by Frank Tallis Hardback Book").
    private static readonly HashSet<string> NotMusicOrFilm = ["book", "hardback", "hardcover", "paperback", "taschenbuch", "buch", "bok",
        "manga", "roman", "novel", "rundhals", "baumwolle", "hoodie", "tshirt", "sweatshirt"];
    private static readonly HashSet<string> MusicWords = ["cd", "album", "musik", "music", "single", "maxi", "ep", "lp", "vinyl", "kassette", "cassette"];

    /// <summary>The word a film is searched with: "dvd", "blu-ray", "vhs" - "" for everything else.</summary>
    public static string SearchFormat(string kind) => kind switch { Kinds.Dvd => "dvd", Kinds.BluRay => "blu-ray", Kinds.Vhs => "vhs", _ => "" };

    public static bool IsTheFilm(Item item, string listing, bool needFormatWord = true)
    {
        var isMusic = item.Kind is Kinds.Cd or Kinds.Vinyl or Kinds.Kassette;
        if ((isMusic || item.Kind is Kinds.Dvd or Kinds.BluRay or Kinds.Vhs) && ListingParse.Words(listing).Any(NotMusicOrFilm.Contains)) return false;
        // A record read without its artist (2026-10-25, "Love Sick"): only listings that say it is music count.
        if (isMusic && item.Artist.Trim() == "")
            return System.Text.RegularExpressions.Regex.Matches(listing.ToLowerInvariant(), @"[\p{L}\p{N}]+").Any(m => MusicWords.Contains(m.Value));
        if (item.Kind is not (Kinds.Dvd or Kinds.BluRay or Kinds.Vhs)) return true;
        var low = " " + listing.ToLowerInvariant() + " ";
        if (CdRx().IsMatch(low) || System.Text.RegularExpressions.Regex.IsMatch(low, @"#\s*\d")) return false;
        // A 3D copy is another edition - unless ours is one.
        if (System.Text.RegularExpressions.Regex.IsMatch(low, @"\b3d\b") && !item.Title.Contains("3D", StringComparison.OrdinalIgnoreCase)) return false;
        // A sequel or another part is another film (2026-10-25: "Guardians Of The Galaxy 2", "Vol. 3" for the first one):
        // a small number in the listing that our title does not have.
        var ours = ListingParse.Words(item.Title).Where(ListingParse.IsNumber).ToHashSet();
        if (!IsBoxSet(item) && ListingParse.Words(listing).Any(w => ListingParse.IsNumber(w) && w.Length <= 2 && w != "0" && !ours.Contains(w))) return false;
        // Searched as "dvd ...": the site already looked for the film, a title without the word still counts.
        if (!needFormatWord) return true;
        return ListingParse.Words(listing).Any(FilmWords.Contains) || System.Text.RegularExpressions.Regex.IsMatch(low, @"\bbd\b|blu-ray|4k");
    }

    // A box set ("Matador Kollektionen", DR boks, "episode 1-13") IS several discs and a collection - for one of those the
    // box words and "1-24" are the thing itself (2026-10-26: Matador and Nynne found nothing at all).
    [System.Text.RegularExpressions.GeneratedRegex(@"boks|\bbox|kollektion|collection|samling|komplet|sæson|season|episode|serie\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex BoxSetRx();
    private static readonly HashSet<string> BoxWords = ["dvds", "boks", "samleboks", "box", "boxset", "collection", "samling", "komplet",
        "complete", "komplett", "sammlung"];

    public static bool IsBoxSet(Item item) =>
        item.Kind is Kinds.Dvd or Kinds.BluRay or Kinds.Vhs && BoxSetRx().IsMatch($"{item.Title} {item.Note} {item.Edition}");

    // "2 Anders and Julekalendere som indlæg" (2026-10-26, priced 45 kr each from one 50 kr listing for two): a count in
    // front is several copies - unless our own title starts with that number.
    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*([2-9]|\d{2})\s+\p{L}")]
    private static partial System.Text.RegularExpressions.Regex CountFirstRx();

    // Anders And blade: the listing must say Anders And (2026-10-26: "Engel i sne af Anders Totland (f. 1986)" and
    // "FN ... af Anders Agner Pedersen (f. 1985)" were kept for blade from 1985/86 - a first name and a year).
    [System.Text.RegularExpressions.GeneratedRegex(@"anders[\s-]*and(?!\p{L})|andersand", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex AndersAndRx();

    // "Jo Nesbø Rød hals, Politi, Genfærd, Panserhjerte, Kakerlakkerne, Kniv" - a list of titles is a bundle: parts after
    // commas that name something of their own (a capital word that is not ours). "Genfærd, Jo Nesbø, genre: krimi" is one.
    private static bool TitleList(Item item, string listing)
    {
        var parts = listing.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 && !ListingParse.Words(listing).Any(w => w == "diverse" || w.EndsWith("pakke"))) return false;
        var mine = ListingParse.Words($"{CleanTitle(item.Title)} {item.Artist} {item.Series} {item.Publisher}").ToHashSet();
        var others = parts.Count(p => System.Text.RegularExpressions.Regex.Matches(p, @"\b\p{Lu}[\p{L}]+")
            .Any(m => m.Value.ToLowerInvariant() is var w && w.Length >= 3 && !mine.Contains(w) && !AboutTheCopy.Contains(w) && !Brands.Contains(w)));
        return others >= 3 || (others >= 2 && ListingParse.Words(listing).Any(w => w == "diverse" || w.EndsWith("pakke")));
    }

    public static bool IsOneCopyOf(Item item, Offer o)
    {
        var words = ListingParse.Words(o.Name);
        var box = IsBoxSet(item);
        if (words.Any(w => NotOneCopy.Contains(w) && !(box && BoxWords.Contains(w))) || (!box && CopyRangeRx().IsMatch(o.Name))
            || RatioRx().IsMatch(o.Name) || PiecesRx().IsMatch(o.Name)) return false;
        if (CountFirstRx().Match(o.Name) is { Success: true } c && !CleanTitle(item.Title).StartsWith(c.Groups[1].Value)) return false;
        if (box ? OtherFormat(item.Kind, string.Join(' ', words.Where(w => !BoxWords.Contains(w)))) : OtherFormat(item.Kind, o.Name)) return false;
        if (CollectorCopy(item, o.Name)) return false;
        if (item.Kind == Kinds.AndersAnd && AndersAndRx().IsMatch($"{item.Title} {item.Series}") && !AndersAndRx().IsMatch(o.Name)) return false;
        if (item.Kind != Kinds.Merch && TitleList(item, o.Name)) return false;
        // "biografplakat", "filmplakat", "kinoposter": the poster, not the film.
        if (item.Kind != Kinds.Merch && words.Any(w => w.Length > 6 && (w.EndsWith("plakat") || w.EndsWith("poster")))) return false;
        if (item.Kind != Kinds.Merch && words.Any(w => NotTheThing.Contains(w) || (w == "magazine" && item.Kind != Kinds.Magasin))) return false;
        if (item.Kind != Kinds.Konsolspil && words.Any(Consoles.Contains)) return false;
        if (!AboutThis(item, words)) return false;
        if (item.Kind != Kinds.Merch && WithSomethingElse(item, o.Name)) return false;
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

    // Not the thing itself but something about it (2026-10-25, Eigil's box: "The Chris Rock Show" found a cap, a zipper pull,
    // a press photo and a magazine ad; "The Face of Defeat" and "The Walking Dead" found Topps cards and a play banknote).
    private static readonly HashSet<string> NotTheThing = ["cap", "caps", "hat", "hoodie", "shirt", "tshirt", "pressefoto", "pressefotos",
        "pressphoto", "card", "cards", "topps", "fleer", "trading", "sticker", "stickers", "klistermærke", "klistermærker", "keychain",
        "nøglering", "schlüsselanhänger", "mug", "zieher", "reissverschluss", "reißverschluss", "billet", "banknote", "print", "anzeige",
        "werbung", "reklame", "patch", "figure", "figur", "actionfigur", "funko",
        // 2026-10-25: "Guardians of the Galaxy also finds puslespil" - and LEGO, plush, costumes, magnets, calendars.
        "puslespil", "puzzle", "puzzles", "pussel", "palapeli", "brikker", "teile", "lego", "byggesæt", "legetøj", "leksak",
        "spielzeug", "toy", "toys", "plush", "plysch", "plyschleksak", "bamse", "tøjdyr", "magnet", "kühlschrankmagnet",
        "køleskabsmagnet", "cosplay", "costume", "kostume", "kostüm", "udklædning", "jacket", "jakke", "jacka", "halsband",
        "halskæde", "necklace", "julekalender", "adventskalender", "plansch", "filmplansch", "matchbox", "lampe",
        "lamp", "tæppe", "blanket", "sengetøj", "pude", "kudde", "kissen", "taske", "rygsæk", "pung", "ur"];
    private static readonly HashSet<string> Consoles = ["xbox", "playstation", "ps2", "ps3", "ps4", "ps5", "nintendo", "wii", "gamecube", "psp"];

    // "Kasket Karl 15 stk", "2 stk tegneserier" - several copies in one listing ("1 stk" is one).
    [System.Text.RegularExpressions.GeneratedRegex(@"\b([2-9]|\d{2,})\s*(stk|styk|pcs|stück|kpl)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex PiecesRx();

    // Kinds whose listings must name this title (and artist/series): books have their own title match on the sites,
    // Anders And, Jumbobøger, games and Magic are named too differently by sellers.
    private static readonly HashSet<string> NamedKinds = [Kinds.Tegneserie, Kinds.Superhelte, Kinds.Cd, Kinds.Vinyl, Kinds.Kassette, Kinds.Dvd,
        Kinds.BluRay, Kinds.Vhs];

    /// <summary>Is the listing about this title at all? (2026-10-25: "Som i et æventyr" found an e-book of other fairy
    /// tales, "Love Sick" by Racetrack Babies a Beanie Baby.) Every word of the main title (before " – " or ":") must be
    /// there - one may be missing from 5 words up; a record or film also needs a word of the artist, a comic a word of
    /// its series when the series is not in the title.</summary>
    public static bool AboutThis(Item item, List<string> listing)
    {
        if (!NamedKinds.Contains(item.Kind)) return true;
        var squashed = string.Concat(listing.Where(w => !ListingParse.IsNumber(w)));
        bool Has(string w) => squashed.Contains(w);
        var title = CleanTitle(item.Title) != "" ? CleanTitle(item.Title) : item.Series;
        var cut = title.IndexOfAny([':', '–']);
        if (cut < 0) cut = title.IndexOf(" - ", StringComparison.Ordinal);
        var main = ListingParse.Words(cut > 2 ? title[..cut] : title).Where(w => !ListingParse.IsNumber(w)).ToList();
        if (main.Count > 0 && main.Count(Has) < (main.Count >= 5 ? main.Count - 1 : main.Count)) return false;
        if (item.Kind is Kinds.Cd or Kinds.Vinyl or Kinds.Kassette or Kinds.Dvd or Kinds.BluRay or Kinds.Vhs)
        {
            var artist = ListingParse.Words(item.Artist).Where(w => !ListingParse.IsNumber(w) && !main.Contains(w)).ToList();
            if (artist.Count > 0 && !artist.Any(Has)) return false;
        }
        if (item.Kind is Kinds.Tegneserie or Kinds.Superhelte)
        {
            var series = ListingParse.Words(item.Series).Where(w => !ListingParse.IsNumber(w) && !Brands.Contains(w)).ToList();
            if (series.Count > 0 && !series.Any(main.Contains) && !series.Any(Has)) return false;
        }
        return true;
    }

    // The product plus something else in one listing (user 2026-10-25: "if the price is for the product plus anything else
    // it should be handled"): "+", "inkl." ... always mean more than the product; "og"/"&"/"with" only when what follows
    // is not part of our own title or artist ("Rum And Coca Cola" stays one product).
    private static readonly HashSet<string> AlwaysMore = ["inkl", "inklusiv", "inklusive", "incl", "including", "inclusive", "plus",
        "samt", "medfølger", "følger", "extras", "ekstra", "zubehör", "tillbehör", "tilbehør"];
    private static readonly HashSet<string> Joins = ["og", "and", "und", "och", "with", "med", "mit"];
    // Words a seller adds about the copy itself - not another product after "og"/"med".
    private static readonly HashSet<string> AboutTheCopy = ["cover", "omslag", "booklet", "hæfte", "cd", "dvd", "case", "æske", "kasse",
        "kassette", "box", "originalt", "original", "orig", "tekst", "tekstark", "plastik", "folie", "kvittering", "stand", "god", "fin",
        "pæn", "ny", "brugt", "ubrugt", "uåbnet", "forseglet", "sealed", "danske", "dansk", "tekster", "undertekster", "subtitles",
        "bonus", "ekstramateriale", "special", "edition", "udgave", "version", "slipcover", "sleeve", "insert", "inlay"];

    /// <summary>Does the listing sell more than this one product?</summary>
    public static bool WithSomethingElse(Item item, string listing)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(listing, @"\s\+\s|\s\+\d|\bm/\s*\w")) return true;
        // "Guardians of the Galaxy / The Amazing Spider-Man 1+2": a slash between titles (not "AC/DC", not "1/2").
        // "Eddie Izzard / Dress To Kill" is artist / title - only a part with two words of its own is another product.
        if (System.Text.RegularExpressions.Regex.IsMatch(listing, @"\s/\s") && !$" {CleanTitle(item.Title)} {item.Artist} ".Contains(" / "))
        {
            var mine = ListingParse.Words($"{CleanTitle(item.Title)} {item.Artist} {item.Series}").ToHashSet();
            if (listing.Split(" / ").Skip(1).Any(part => ListingParse.Words(part)
                    .Count(w => !ListingParse.IsNumber(w) && !mine.Contains(w) && !AboutTheCopy.Contains(w)) >= 2))
                return true;
        }
        var all = System.Text.RegularExpressions.Regex.Matches(listing.ToLowerInvariant(), @"[\p{L}\p{N}]+").Select(m => m.Value).ToList();
        if (all.Any(AlwaysMore.Contains)) return true;
        var ours = ListingParse.Words($"{CleanTitle(item.Title)} {item.Artist} {item.Series}").ToHashSet();
        var raw = $" {CleanTitle(item.Title)} {item.Artist} {item.Series} ".ToLowerInvariant();
        for (var i = 0; i < all.Count - 1; i++)
        {
            var w = all[i];
            if (!Joins.Contains(w) || raw.Contains($" {w} ")) continue;
            var after = all.Skip(i + 1).Where(x => x.Length >= 3 && !ListingParse.IsNumber(x)).ToList();
            var other = after.Where(x => !ours.Contains(x) && !AboutTheCopy.Contains(x)).ToList();
            if (other.Count >= 2) return true;
        }
        // "Guardians of the Galaxy & Avengers": an ampersand before words that are not ours.
        var amp = listing.IndexOf(" & ", StringComparison.Ordinal);
        if (amp > 0 && !raw.Contains(" & "))
        {
            var after = ListingParse.Words(listing[(amp + 3)..]).Where(x => !ListingParse.IsNumber(x)).ToList();
            if (after.Count(x => !ours.Contains(x) && !AboutTheCopy.Contains(x)) >= 2) return true;
        }
        return false;
    }

    /// <summary>"https://www.ebay.de/itm/306342744192?_skw=...&hash=..." -> "https://www.ebay.de/itm/306342744192": the
    /// search and tracking bits are no part of the listing.</summary>
    public static string CleanUrl(string url) =>
        System.Text.RegularExpressions.Regex.Match(url, @"^(https?://[^/]*ebay\.[a-z.]+/itm/\d+)") is { Success: true } m ? m.Groups[1].Value : url;

    // The same eBay listing is shown on ebay.de and ebay.com - one key for both.
    public static string ListingKey(string url) =>
        System.Text.RegularExpressions.Regex.Match(url, @"ebay\.[a-z.]+/itm/(\d+)") is { Success: true } m ? "ebay:" + m.Groups[1].Value : CleanUrl(url);

    /// <summary>The site's answer with only the listings that are one ordinary copy of this item.</summary>
    public static Listings OnlyThisItem(Item item, Listings r, string source = "", bool searchedWithFormat = false)
    {
        if (r.Ambiguous) return r;
        var formatSure = FormatSure.Contains(source);
        var kept = r.All.Where(o => IsOneCopyOf(item, o) && (formatSure || IsTheFilm(item, o.Name, needFormatWord: !searchedWithFormat))).ToList();
        return kept.Count == r.All.Count ? r : ListingParse.Summarise(kept, r.Url) with { Want = r.Want, Have = r.Have };
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
        // "Rock The Chris Rock Show" (2026-10-25): the name is in the title already.
        if (surname != "" && ListingParse.Words(title).Contains(surname.ToLowerInvariant())) return title.Trim();
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
    public static readonly string[] DanishSources = ["DBA", "GulogGratis", "Nemos Bibliotek", "Faraos", "Accord"];
    public const int DanishEnough = 2;

    // Shown as links, never part of our price (user 2026-10-21: "ebay DE is not realistic is it?"): eBay is shops' asking
    // prices, many from abroad, and the API only shows what is for sale, not what sold - a used Californication CD had a
    // middle price of 291 kr there.
    // Discogs (2026-10-25, user: Danish + Tradera + one eBay for prices): it tells which record it is and how many
    // collectors want it - its asking prices are shown as links only.
    public static readonly string[] NotForNormal = ["eBay", "eBay USA", "Discogs"];

    /// <summary>The normal used price with Danish listings first (user 2026-10-12: "always prioritize Danish market"):
    /// Danish only when there are enough - unless the Danish price is over 2x what the other sites say, then the Danish
    /// listings are the odd ones (2026-10-21) and every site counts. eBay never counts.</summary>
    public static int? NormalDanishFirst(IReadOnlyCollection<(string Source, int Kr)> used)
    {
        var counted = used.Where(u => !NotForNormal.Contains(u.Source)).ToList();
        var danish = counted.Where(u => DanishSources.Contains(u.Source)).ToList();
        var all = Realistic(counted);
        if (danish.Count < DanishEnough) return all;
        var d = Realistic(danish);
        var others = counted.Where(u => !DanishSources.Contains(u.Source)).ToList();
        return others.Count >= DanishEnough && Realistic(others) is { } o && d > o * 2 ? all : d;
    }

    /// <summary>What it really sells for (user 2026-10-21: "our price should be an algorithm based on NOT the unrealistic
    /// ones"): asking prices over 2x the middle are left out as unrealistic, then each site gets one vote - its own
    /// middle price - and the price is the middle of those votes (the lower one when it is even). One site full of odd
    /// listings can no longer decide the price on its own.</summary>
    public static int? Realistic(IReadOnlyCollection<(string Source, int Kr)> used)
    {
        var priced = used.Where(u => u.Kr > 0).ToList();
        if (priced.Count == 0) return null;
        var middle = LowerMiddle(priced.Select(u => u.Kr).ToList());
        var votes = priced.Where(u => u.Kr <= middle * 2).GroupBy(u => u.Source)
            .Select(g => LowerMiddle(g.Select(u => u.Kr).ToList())).ToList();
        return LowerMiddle(votes);
    }

    private static int LowerMiddle(List<int> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        return sorted[(sorted.Count - 1) / 2];
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
public sealed record HuntResult(List<PriceCheck> Checks, List<PriceFind> Finds, int Answered, int? Normal, int? SimilarNormal = null, int? NewNormal = null,
    int? Want = null, int? Have = null)
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
                Original = o.Original, Url = Pricing.CleanUrl(o.Url), FoundAt = now,
            }));
        }
        // Every matching listing counts for the price (the kept ones are only a sample for the links).
        var used = answers.Where(a => !a.IsNew && a.Result is { Ambiguous: false })
            .SelectMany(a => a.Result!.All.Where(o => o.Price is > 0).DistinctBy(o => (o.Url, o.Price))
                .Where(o => a.Source != "ZVAB" || !abeIds.Contains(Pricing.ListingNumber(o.Url)))
                .Select(o => (a.Source, Kr: (int)o.Price!.Value))).ToList();

        // "Magen til": kept apart, flagged, and never part of the normal price.
        var like = (similar ?? []).Where(a => !a.IsNew && a.Result is { Ambiguous: false }).ToList();
        foreach (var a in like)
            finds.AddRange(Pricing.Keep(a.Result!).Select(o => new PriceFind
            {
                ItemId = itemId, Source = a.Source, Name = Cut(o.Name, 300), Kr = (int)o.Price!.Value, Original = o.Original,
                Url = Pricing.CleanUrl(o.Url), FoundAt = now, Similar = true,
            }));
        // One link per listing (the same eBay listing came from both ebay.de and ebay.com).
        finds = finds.DistinctBy(f => (Pricing.ListingKey(f.Url), f.Similar)).ToList();
        var likeKr = like.SelectMany(a => Pricing.Keep(a.Result!)).Select(o => (int)o.Price!.Value).ToList();
        // New shops (Saxo, Amazon ...): the price a new copy is compared with.
        var fresh = answers.Where(a => a.IsNew && a.Result is { Ambiguous: false }).SelectMany(a => Kept(a).Select(o => (int)o.Price!.Value)).ToList();
        // Discogs want/have: from the first source that knows it (only Discogs today).
        var demand = answers.Select(a => a.Result).FirstOrDefault(r => r?.Want is not null);
        return new HuntResult(checks, finds, answers.Count(a => a.Result is not null), Pricing.NormalDanishFirst(used), Pricing.NormalOf(likeKr),
            Pricing.NormalOf(fresh), demand?.Want, demand?.Have);
    }

    // Onto the item: when it was hunted (nothing answered = try again tomorrow, not in 30 days) and the price hint. A new
    // copy is priced 10 % under the new shops (2026-10-20); used prices only when no new shop has it. The hint is worked
    // out again each time - no realistic listings now = no hint, not an old one. Not sure what the thing is (under
    // Pricing.MinConfidence, e.g. "Lead Data" = a blank CD-R read as an album) = no hint at all; a person looks first.
    public void ApplyTo(Item item, DateTime now)
    {
        item.PricesAt = Answered == 0 ? now - Pricing.StaleAfter + TimeSpan.FromDays(1) : now;
        if (Want is not null) (item.Want, item.Have) = (Want, Have);
        if (Answered == 0) return;
        if (item.Confidence < Pricing.MinConfidence && !Finds.Any(f => !f.Similar && !f.IsNew && Pricing.ConfirmsReading(item, f.Name)))
        { item.PriceHint = null; item.SimilarHint = null; return; }
        var market = item.Usage == Usages.New ? NewNormal ?? Normal : Normal;
        item.PriceHint = ShopPrice(market);
        item.SimilarHint = Normal is null ? SimilarNormal : null;
    }

    private static int? ShopPrice(int? normal) => Pricing.ShopPrice(normal);
    private static int? Kr(decimal? v) => v is null ? null : (int)Math.Round(v.Value);
    private static string Cut(string s, int n) => s.Length <= n ? s : s[..n];
}
