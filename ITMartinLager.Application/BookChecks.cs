using ITMartinLager.Domain;

namespace ITMartinLager.Application;

/// <summary>One book on the Bøger page while it is being checked (user 2026-10-07: "take a picture of books ... and see
/// which books are interesting - price and efterspørgsel").</summary>
public sealed class CheckedBook
{
    public bool Keep = true, Looking;
    public string Title = "", Author = "", Condition = "God", Note = "", Barcode = "";
    public int? Year;
    public int Quantity = 1, PhotoId;
    public double Confidence = 1;
    /// <summary>Per site (BookCheckUseCases.Sites): what it has for sale. Missing = not asked yet, null = did not answer.</summary>
    public Dictionary<string, Listings?> Sites = [];
    public BookValue? Value;
    public int CheckId;

    public Listings? Site(string name) => Sites.GetValueOrDefault(name);
    public bool Asked => Sites.Count > 0;

    // The listings behind the price, from the sites the AI based it on.
    public IEnumerable<(string Source, Offer Offer)> Proof() =>
        (Value?.Basis ?? []).SelectMany(s => (Site(s)?.Proof ?? []).Select(o => (s, o)));

    public static CheckedBook From(Found f, int photoId = 0) => new()
    {
        Title = f.Title, Author = f.Artist, Year = f.Year, Condition = f.Condition, Quantity = f.Quantity,
        Confidence = f.Confidence, Note = f.Note, PhotoId = photoId, Barcode = f.Barcode,
    };

    // The line the AI values: the book plus what every site has.
    public string Line()
    {
        var parts = new List<string> { Author, Title, Year?.ToString() ?? "", "stand: " + Condition }.Where(x => x.Trim() != "").ToList();
        if (Barcode != "") parts.Add("ISBN " + Barcode);
        parts.AddRange(BookCheckUseCases.Sites.Select(s => s + ": " + (Site(s)?.Text ?? "ukendt")));
        return string.Join(" · ", parts);
    }

    /// <summary>The market: the middle of the used asking prices on all the sites (prices over 3x the middle left out).</summary>
    public int? Market() => Pricing.NormalOf(Sites.Values.Where(l => l is { Ambiguous: false })
        .SelectMany(l => l!.All).Where(o => o.Price is > 0).Select(o => (int)o.Price!.Value).ToList());

    /// <summary>Our price in the shop: 10 % under the market. Null = no site has it - then nobody can prove a price.</summary>
    public int? ShopPrice() => Pricing.ShopPrice(Market());

    public int Rank => Value?.Verdict switch { "Sælg" => 0, BookValue.Unsure => 1, "Kasse" => 2, "Genbrug" => 3, _ => 4 };

    // What is saved on the item when the book goes into a box.
    public Found ToFound(DateTime today) => new(
        Kinds.Bog, Title.Trim(), Author.Trim(), "", "", Year, "", Conditions.Normalise(Condition),
        Math.Max(1, Quantity), Confidence, SaveNote(today), "", Barcode,
        ShopPrice() is { } p ? p : Value is { Low: { } lo, High: { } hi } ? (lo + hi) / 2m : null);

    private string SaveNote(DateTime today)
    {
        var parts = new List<string>();
        if (ShopPrice() is { } p) parts.Add($"Vores pris {p} kr (10 % under markedet ca. {Market()} kr)");
        if (Value is { } v) parts.Add($"{v.Verdict}" + (v.Low is { } lo ? $" · {lo}-{v.High} kr" : "") + $" · efterspørgsel {v.Demand} · {v.Reason}");
        foreach (var s in new[] { "DBA", "AbeBooks", "Bokbörsen" })
            if (Site(s) is { Count: > 0 } l) parts.Add($"{s} {today:d/M}: {l.Text}");
        if (Note != "") parts.Add(Note);
        return string.Join(" | ", parts);
    }

    public void FillCheck(BookCheck c, DateTime now)
    {
        c.Title = Title.Trim(); c.Author = Author.Trim(); c.Year = Year; c.Condition = Condition; c.Isbn = Barcode;
        c.Quantity = Quantity; c.PhotoId = PhotoId == 0 ? null : PhotoId;
        c.Verdict = Value!.Verdict; c.Low = Value.Low; c.High = Value.High; c.Demand = Value.Demand;
        c.SellAt = Value.Where; c.Reason = Value.Reason;
        c.Dba = Site("DBA")?.Text ?? ""; c.AbeBooks = Site("AbeBooks")?.Text ?? ""; c.Bokborsen = Site("Bokbörsen")?.Text ?? "";
        c.Nemos = Site("Nemos Bibliotek")?.Text ?? ""; c.Faraos = Site("Faraos")?.Text ?? "";
        c.Proof = string.Join("\n", Proof().Select(p => $"{p.Source}: {p.Offer.Name} – {p.Offer.Price:0} kr – {p.Offer.Url}"));
        c.CheckedAt = now;
        c.Touch();
    }
}

/// <summary>Valuing books: the sites first, then ONE AI call for up to 40 books with the sites' numbers in it.
/// Every valued book is kept in "Alle tjekkede bøger" (user 2026-10-07: "save all automatically").</summary>
public sealed class BookCheckUseCases(
    IEnumerable<IPriceSource> sources, IBookValuer valuer, IBookCheckRepository checks, RegistrationUseCases registration,
    IVareinfoGateway vareinfo, TimeProvider clock)
{
    /// <summary>The sites the Bøger page asks, in this order.</summary>
    public static readonly string[] Sites = ["DBA", "AbeBooks", "Bokbörsen", "Nemos Bibliotek", "Faraos"];
    public const int PerValueCall = 40;
    /// <summary>How long the Bøger page waits for the sites before valuing with what has answered (user 2026-10-12:
    /// "it was saying vurderes a long time"). A site that has not answered by then counts as "ukendt".</summary>
    public static readonly TimeSpan LookUpLimit = TimeSpan.FromSeconds(30);

    public int MaxCallsPerDay => valuer.MaxCallsPerDay;
    public Task<int> CallsTodayAsync(CancellationToken ct = default) => valuer.CallsTodayAsync(ct);

    /// <summary>The titles on a photo of a pile - read by Vareinfo, which also keeps the photo.</summary>
    public Task<(int PhotoId, List<Found> Found)> ReadPileAsync(Stream photo, CancellationToken ct = default) =>
        vareinfo.ReadPileAsync(photo, "bøger", "boeger", ct);

    public Task<Found?> LookupBarcodeAsync(string code, CancellationToken ct = default) => vareinfo.LookupBarcodeAsync(code, ct);

    private readonly List<IPriceSource> _sites = sources.Where(s => Sites.Contains(s.Name)).OrderBy(s => Array.IndexOf(Sites, s.Name)).ToList();

    public async Task LookUpAsync(CheckedBook b, CancellationToken ct = default)
    {
        b.Looking = true;
        var asked = _sites.Select(s => (s.Name, s.LookupAsync(b.Title, b.Author, b.Barcode, ct))).ToList();
        var sites = new Dictionary<string, Listings?>();
        foreach (var (name, lookup) in asked)
        {
            // Past the deadline (ct cancelled) or failed = did not answer; the book is still valued with the rest.
            try { sites[name] = await lookup; }
            catch (Exception) { sites[name] = null; }
        }
        b.Sites = sites;
        b.Looking = false;
    }

    /// <summary>Values books that have sites but no value yet, 40 per call, and keeps them in the record.</summary>
    public async Task ValueAsync(IReadOnlyList<CheckedBook> books, CancellationToken ct = default)
    {
        foreach (var chunk in books.Where(b => b.Value is null && b.Title.Trim() != "").Chunk(PerValueCall))
        {
            var values = await valuer.ValueBooksAsync(chunk.Select(b => b.Line()).ToList(), ct);
            for (var i = 0; i < chunk.Length; i++)
                if (values.TryGetValue(i, out var raw))
                {
                    var b = chunk[i];
                    b.Value = raw.Proven(s => b.Site(s)?.HasPrices == true);
                    if (b.Value.Verdict == "Genbrug") b.Keep = false;
                }
            await RememberAsync(chunk, ct);
        }
    }

    /// <summary>Saved the moment a book has its value - a reload or a closed phone never loses a checked book.</summary>
    public async Task RememberAsync(IEnumerable<CheckedBook> books, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var saved = new List<(CheckedBook Book, BookCheck Check)>();
        foreach (var b in books.Where(b => b.Value is not null))
        {
            var c = (b.CheckId != 0 ? await checks.GetAsync(b.CheckId, ct) : null) ?? new BookCheck();
            b.FillCheck(c, now);
            saved.Add((b, c));
        }
        await checks.SaveAsync(saved.Select(s => s.Check), ct);
        foreach (var (b, c) in saved) b.CheckId = c.Id;
    }

    /// <summary>The ticked books into a box (merged like any registration), and the record says which box.</summary>
    public async Task<(int Added, int Merged)> SaveInBoxAsync(Box? box, IReadOnlyList<CheckedBook> books, CancellationToken ct = default)
    {
        int added = 0, merged = 0;
        var today = clock.GetLocalNow().DateTime;
        foreach (var g in books.Where(b => b.Keep && b.Title.Trim() != "").GroupBy(b => b.PhotoId))
        {
            var (a, m) = await registration.SaveAsync(box?.Id, g.Key == 0 ? null : g.Key, g.Select(b => b.ToFound(today)), ct);
            added += a; merged += m;
        }
        await RememberAsync(books, ct);   // corrections made after the valuation go into the record too
        var kept = books.Where(b => b.Keep && b.CheckId != 0).Select(b => b.CheckId).ToList();
        if (kept.Count > 0) await checks.SetBoxAsync(kept, box?.Code ?? "på lager", ct);
        return (added, merged);
    }

    public async Task<(List<BookCheck> Rows, int Total)> HistoryAsync(string text, CancellationToken ct = default) =>
        (await checks.SearchAsync(text, 50, ct), await checks.CountAsync(ct));

    /// <summary>Links for a person to check the book themselves.</summary>
    public List<(string Name, string Url)> Links(CheckedBook b)
    {
        var query = Pricing.QueryFor(b.Title, b.Author);
        var links = _sites.Select(s => (s.Name == "Nemos Bibliotek" ? "Nemos" : s.Name, s.SearchUrl(b.Title, b.Author, b.Barcode))).ToList();
        links.Add(("eBay solgte", $"https://www.ebay.com/sch/i.html?_nkw={Uri.EscapeDataString(query)}&LH_Sold=1&LH_Complete=1"));
        links.Add(("antikvariat.net", $"https://www.antikvariat.net/da/actions/biblio-search/default/search?author={Uri.EscapeDataString(b.Author)}&title={Uri.EscapeDataString(b.Title)}&currency=DKK"));
        links.Add(("Bogbasen", $"https://bogbasen.dk/search?forfatter={Uri.EscapeDataString(b.Author)}&titel={Uri.EscapeDataString(b.Title)}"));
        return links;
    }
}
