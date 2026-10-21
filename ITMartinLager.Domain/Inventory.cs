namespace ITMartinLager.Domain;

// Kinds, Conditions, EditionHints, BoxCodes, Barcodes and Found are the shared words in ITMartinVarer.Contracts.

// Has the price hunter valued it? (user 2026-10-12: "it should be the type of product and a IsValued flag ... and the
// valued price on the sites" - instead of choosing a box). Worked out from the item, never stored.
public static class Valuations
{
    public const string Valued = "Vurderet", Waiting = "Venter på pris", NoPrice = "Ingen pris fundet";
    public static readonly string[] All = [Valued, Waiting, NoPrice];
}

public static class Statuses
{
    public const string Lager = "På lager", TilSalg = "Til salg", PaaAuktion = "På auktion", Solgt = "Solgt";
    public static readonly string[] All = [Lager, TilSalg, PaaAuktion, Solgt];
}

// A box, shelf or crate with a printed QR label - the only "where is it" the register needs.
public sealed class Box
{
    public int Id { get; set; }
    public string Code { get; set; } = "";       // K0001 - on the label
    public string Place { get; set; } = "";      // "Garagen, reol 3"
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int Number => BoxCodes.Number(Code);

    public static string CodeFor(int n) => BoxCodes.For(n);

    // The next code after the last one: K0001, K0002 ...
    public static string NextCode(string? last) => BoxCodes.Next(last);
}

public sealed class Item
{
    public int Id { get; set; }
    public string Kind { get; set; } = Kinds.Andet;
    public string Title { get; set; } = "";
    public string Series { get; set; } = "";     // "Anders And & Co.", "Fantomet", "PlayStation 2"
    public string Number { get; set; } = "";     // issue / volume / card number, as printed
    public int? Year { get; set; }
    public string Platform { get; set; } = "";   // console games
    public string Artist { get; set; } = "";     // CDs (and author for books)
    public string Condition { get; set; } = "God";
    public string Usage { get; set; } = Usages.Used;   // Brugt / Ny (Usages) - new = never sold before, priced against new prices
    // Discogs collectors (2026-10-21): how many want this release and how many have it - demand, not price.
    public int? Want { get; set; }
    public int? Have { get; set; }
    public int Quantity { get; set; } = 1;
    public int? BoxId { get; set; }
    public Box? Box { get; set; }
    public int? PhotoId { get; set; }
    public decimal? Price { get; set; }          // kr, set by a person
    public decimal? PriceHint { get; set; }      // kr, the suggested SHOP price: 10 % under the other sites (Pricing.ShopPrice)
    public decimal? SimilarHint { get; set; }    // kr, the middle price of "magen til" when nothing exact is for sale - a pointer, not a price
    public string MagicRef { get; set; } = "";
    public string Barcode { get; set; } = "";    // EAN/ISBN when scanned   // card id in the MTG Scanner, so a new fetch updates instead of duplicating
    public string Status { get; set; } = Statuses.Lager;
    public string Note { get; set; } = "";
    // What makes it interesting - the shop's focus (user 2026-10-05: "it is the interest we focus on"), shown even
    // when the item is cheap: who drew it, which story is in it, what was special that year. AI draft, edited by hand.
    public string Interest { get; set; } = "";
    public double Confidence { get; set; } = 1;
    public string ItemPhoto { get; set; } = "";  // own photo of just this item (for auctions/shop), relative to the data dir

    // Sales. AuctionItemId = the item's id in Live Auktion while it is up for auction; the result is copied here,
    // because Live Auktion deletes an auction 7 days after it was created.
    public string AuctionItemId { get; set; } = "";
    public string AuctionCode { get; set; } = "";
    public decimal? SoldPrice { get; set; }
    public string SoldTo { get; set; } = "";
    public DateTime? SoldAt { get; set; }  // how sure the AI was; < 0.6 = check it
    public string Search { get; set; } = "";     // lower-case title + series + number + year, for LIKE search
    public DateTime? PricesAt { get; set; }      // when the price hunter last searched every source for it
    // Kolofon (2026-10-11, user: "to save in db and get price just cover. A job can run through kolofon if needed"):
    // a photo of the title page's back, only for books whose prices say the edition matters; KolofonReader reads it.
    public string KolofonPhoto { get; set; } = "";   // relative to the data dir
    public DateTime? KolofonAt { get; set; }         // when the AI read it - null = waiting
    public string Edition { get; set; } = "";        // "1. udgave, 1. oplag"
    public string Publisher { get; set; } = "";
    public string EditionHint { get; set; } = "";    // EditionHints - from the cover
    // "Vis på forsiden" (user 2026-10-15: "the app that handles the products need to put products onto Homepage" - not a
    // webshop feel, interest oriented): chosen by a person, shown on bogshoppen.dk and itmartin.dk as "Fundet i kasserne".
    public DateTime? FeaturedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // "Anders And & Co. · nr. 12 · 1968" - how an item is named everywhere (lists, auctions, labels).
    public string Line() => string.Join(" · ", new[]
    {
        Artist, Title,
        Series != "" && !Title.Contains(Series, StringComparison.OrdinalIgnoreCase) ? Series : "",
        Number != "" ? "nr. " + Number : "", Year?.ToString() ?? "", Platform,
    }.Where(s => !string.IsNullOrWhiteSpace(s)));

    // The item photo's name in a download (user 2026-10-19): id first (unique, sorts), then what it is. Only characters
    // Windows, Mac and phones all accept; the server is Linux, so Path.GetInvalidFileNameChars is not enough.
    public string PhotoFileName()
    {
        var text = new string(Line().Select(c => char.IsControl(c) || "\\/:*?\"<>|·".Contains(c) ? ' ' : c).ToArray());
        text = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        if (text.Length > 80) text = text[..80].TrimEnd(' ', '.');
        return text == "" ? $"{Id}.jpg" : $"{Id} {text}.jpg";
    }

    public string Valuation => PriceHint is > 0 ? Valuations.Valued : PricesAt is null ? Valuations.Waiting : Valuations.NoPrice;

    // Same thing in the same condition = one row with a higher quantity, so 7 copies of Anders And 12/1968 stay one
    // line however many photos they turned up in. Whether a second photo IS a second copy, the person decides in Vareinfo.
    public bool SameAs(Found f) =>
        Kind == f.Kind && Condition == f.Condition && Usage == Usages.Normalise(f.Usage) && Year == f.Year
        && Eq(Title, f.Title) && Eq(Artist, f.Artist) && Eq(Series, f.Series) && Eq(Number, f.Number) && Eq(Platform, f.Platform);

    // "Is this the thing we already have?" for a photo taken again: only what identifies it - the title (case, spaces and
    // punctuation ignored: "Start-Up: 5-String Banjo" = "Start-Up 5-String Banjo"), and number and year when both have
    // one. Condition, series, author and kind are read a little differently from photo to photo (2026-10-12: the same
    // pile photographed twice matched only 4 of 8 on the strict SameAs).
    // A scanned barcode gives the publisher's full title ("Absolute Beginners Keyboard: The Complete Picture Guide ...")
    // where the photo gave the cover's - so one title starting with the other counts too, and the same barcode always.
    public bool LooksLike(Found f)
    {
        if (Barcode != "" && f.Barcode != "" && Barcode == f.Barcode) return true;
        string a = TitleKey(Title), b = TitleKey(f.Title);
        var titles = a != "" && b != "" && (a == b || (Math.Min(a.Length, b.Length) >= 8 && (a.StartsWith(b) || b.StartsWith(a))));
        return titles
            && (Number.Trim() == "" || f.Number.Trim() == "" || Eq(Number, f.Number))
            && (Year is null || f.Year is null || Year == f.Year);
    }

    /// <summary>A scan of something we registered from a photo: the item gets the barcode (an ISBN makes the price
    /// hunter search again for exactly that edition). False when it already had one.</summary>
    public bool GiveBarcode(string code)
    {
        if (Barcode != "" || string.IsNullOrWhiteSpace(code)) return false;
        Barcode = code.Trim();
        if (Barcodes.IsIsbn(Barcode)) PricesAt = null;
        Touch();
        return true;
    }

    /// <summary>Another row of what looks like the same thing (other box, other condition) - "Vi har flere".</summary>
    public bool LooksLike(Item o) => o.Id != Id && LooksLike(new Found(o.Kind, o.Title, o.Artist, o.Series, o.Number, o.Year,
        o.Platform, o.Condition, o.Quantity, o.Confidence, "", Barcode: o.Barcode));

    public static string TitleKey(string title) => new(title.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static bool Eq(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static Item From(Found f, int? boxId, int? photoId)
    {
        var item = new Item
        {
            Kind = f.Kind, Title = f.Title, Artist = f.Artist, Series = f.Series, Number = f.Number, Year = f.Year,
            Platform = f.Platform, Condition = f.Condition, Quantity = f.Quantity, Confidence = f.Confidence,
            Note = f.Note, Interest = f.Interest, Barcode = f.Barcode, PriceHint = f.PriceHint, EditionHint = f.EditionHint,
            Usage = Usages.Normalise(f.Usage),
            BoxId = boxId, PhotoId = photoId,
        };
        item.Touch();
        return item;
    }

    /// <summary>One copy is sold: it leaves the stock, and the last one makes the row Solgt (the row keeps saying what
    /// was sold, for whom and for how much). how = "i butikken for 120 kr (ordre 7)".</summary>
    public void SellCopy(int kr, string buyer, DateTime now, string how)
    {
        Quantity = Math.Max(0, Quantity - 1);
        if (Quantity == 0)
        {
            Status = Statuses.Solgt;
            SoldPrice = kr;
            SoldTo = buyer;
            SoldAt = now;
            Quantity = 1;
        }
        Note = (Note + $"\nSolgt {now:yyyy-MM-dd} {how}").Trim();
        Touch();
    }

    public void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
        Search = string.Join(' ', Title, Artist, Series, Number, Year?.ToString() ?? "", Platform, Kind, Note, Interest, Barcode, Edition, Publisher, EditionHint).ToLowerInvariant();
    }
}

// One registration photo (kept, so a wrong reading can be checked against the picture).
public sealed class Photo
{
    public int Id { get; set; }
    public string Path { get; set; } = "";       // relative to the data dir
    public int? BoxId { get; set; }
    public int Found { get; set; }
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
}

// Every book that has been valued on the Bøger page, kept or not (user 2026-10-07: "save all automatically") -
// a searchable record of what was checked and what it was worth, apart from the box register (Items).
public sealed class BookCheck
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int? Year { get; set; }
    public string Condition { get; set; } = "God";
    public string Isbn { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public int? PhotoId { get; set; }
    public string Verdict { get; set; } = "";    // Sælg / Kasse / Genbrug / Tjek selv
    public int? Low { get; set; }                // kr, null = Tjek selv
    public int? High { get; set; }
    public string Demand { get; set; } = "";
    public string SellAt { get; set; } = "";     // DBA / AbeBooks / Bokbörsen / eBay / Bogshoppen
    public string Reason { get; set; } = "";
    public string Dba { get; set; } = "";        // the listing texts as shown, at the time of the check
    public string AbeBooks { get; set; } = "";
    public string AmazonDe { get; set; } = "";   // only on checks before 2026-10-12 (Amazon.de dropped: new prices, no use)
    public string Bokborsen { get; set; } = "";
    public string Nemos { get; set; } = "";
    public string Faraos { get; set; } = "";
    public string Proof { get; set; } = "";      // the listings behind the price: "DBA: name – 45 kr – url", one per line
    public string BoxCode { get; set; } = "";    // set when it was saved into a box
    public string Search { get; set; } = "";
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    public void Touch() => Search = string.Join(' ', Title, Author, Year?.ToString() ?? "", Isbn, Verdict, SellAt).ToLowerInvariant();
}

// What one source said about one item the last time the price hunter asked (2026-10-10, user: "as many places as
// possible"). Ok = false: the site could not be asked (blocked, down) - not the same as "ingen til salg".
public sealed class PriceCheck
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string Source { get; set; } = "";
    public bool IsNew { get; set; }              // a shop's new price, not a used copy
    public bool Ok { get; set; }
    public int Count { get; set; }               // listings that matched the title
    public int? Low { get; set; }                // kr, the realistic asking prices (over 3x the middle left out)
    public int? Median { get; set; }
    public int? High { get; set; }
    public int? Wild { get; set; }               // highest "urimelig internetpris"
    public bool Ambiguous { get; set; }          // title too broad to know the listings are this book
    public string SearchUrl { get; set; } = "";
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

// One listing found for an item - the proof: "Fundet på ZVAB til 70 kr (9,37 EUR) · link".
public sealed class PriceFind
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string Source { get; set; } = "";
    public bool IsNew { get; set; }
    public string Name { get; set; } = "";       // the listing's own title, so a wrong match can be seen
    public int Kr { get; set; }
    public string Original { get; set; } = "";   // "9,37 EUR" when the site is not in kr
    public string Url { get; set; } = "";
    public DateTime FoundAt { get; set; } = DateTime.UtcNow;
    // "Magen til" (user 2026-10-12): not this exact one - the same title in another årgang/issue/edition. Shown as a
    // pointer when nothing exact is for sale, never used as proof for our price.
    public bool Similar { get; set; }
}

// AI calls per day - the hard cap on spend lives in the database, so a restart does not reset it.
public sealed class AiDay
{
    public DateOnly Day { get; set; }
    public int Calls { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
}

