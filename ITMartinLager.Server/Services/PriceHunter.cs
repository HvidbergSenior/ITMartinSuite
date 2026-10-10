using ITMartinLager.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Server.Services;

// Asks every price source about one item and keeps what they said (2026-10-10, user: "as many places as possible ...
// facts are needed like 'found on X til 45 kr. Link'"). No AI - only the sites' own listings, each with its link.
public sealed class PriceHunt(
    IDbContextFactory<LagerDb> dbf, DbaListings dba, AbeBooksListings abe, ZvabListings zvab, AmazonDeListings amazon,
    NemosListings nemos, FaraosListings faraos, BokborsenListings bokborsen, BogIdeListings bogIde)
{
    // Books and paper - Magic cards get their price from the MTG Scanner, CDs/DVDs/games have no source here yet.
    public static readonly string[] HuntedKinds = [Kinds.Bog, Kinds.Tegneserie, Kinds.Jumbobog, Kinds.AndersAnd, Kinds.Magasin];

    private const int KeepPerSource = 12;

    // Name, new price (true) or used market, and the lookup. Amazon.de shows the shop's (new) price first.
    private IEnumerable<(string Name, bool IsNew, Task<Listings?> Lookup)> Ask(string title, string author, string isbn, CancellationToken ct) =>
    [
        ("DBA", false, dba.LookupAsync(title, author, ct)),
        ("AbeBooks", false, abe.LookupAsync(title, author, isbn, ct)),
        ("ZVAB", false, zvab.LookupAsync(title, author, isbn, ct)),
        ("Bokbörsen", false, bokborsen.LookupAsync(title, author, isbn, ct)),
        ("Nemos Bibliotek", false, nemos.LookupAsync(title, author, ct)),
        ("Faraos", false, faraos.LookupAsync(title, author, ct)),
        ("Amazon.de", true, amazon.LookupAsync(title, author, isbn, ct)),
        ("Bog & idé", true, bogIde.LookupAsync(title, author, isbn, ct)),
    ];

    public static string Isbn(string barcode) => BarcodeLookup.IsIsbn(barcode) ? barcode : "";

    // Comics are sold by issue: "Anders And & Co." alone is a thousand different blade.
    public static string TitleFor(Item i)
    {
        var t = i.Title.Trim() != "" ? i.Title.Trim() : i.Series.Trim();
        return i.Number != "" && !ListingParse.Words(t).Contains(i.Number.Trim().ToLowerInvariant()) ? $"{t} {i.Number.Trim()}" : t;
    }

    // Returns how many sources answered. The sites are all different, so they are asked at the same time;
    // each source keeps its own limit of requests at a time.
    public async Task<int> HuntAsync(int itemId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var item = await db.Items.FindAsync([itemId], ct);
        if (item is null) return 0;
        var title = TitleFor(item);
        var asked = Ask(title, item.Artist, Isbn(item.Barcode), ct).ToList();
        var answers = new List<(string Name, bool IsNew, Listings? Result)>();
        foreach (var (name, isNew, lookup) in asked)
        {
            Listings? r = null;
            try { r = await lookup; } catch (Exception ex) when (ex is not OperationCanceledException) { }
            answers.Add((name, isNew, r));
        }

        // ZVAB is AbeBooks' German front - the same listing (same number) is shown once, under AbeBooks.
        var abeIds = answers.Where(a => a.Name == "AbeBooks" && a.Result is not null)
            .SelectMany(a => a.Result!.All).Select(o => ListingNumber(o.Url)).Where(n => n != "").ToHashSet();

        await db.PriceChecks.Where(c => c.ItemId == itemId).ExecuteDeleteAsync(ct);
        await db.PriceFinds.Where(f => f.ItemId == itemId).ExecuteDeleteAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var (name, isNew, r) in answers)
        {
            db.PriceChecks.Add(new PriceCheck
            {
                ItemId = itemId, Source = name, IsNew = isNew, Ok = r is not null, Count = r?.Count ?? 0,
                Low = Kr(r?.Low), Median = Kr(r?.Median), High = Kr(r?.High), Wild = Kr(r?.Wild), Ambiguous = r?.Ambiguous ?? false,
                SearchUrl = r?.Url ?? "", CheckedAt = now,
            });
            if (r is null || r.Ambiguous) continue;
            foreach (var o in Keep(r).Where(o => name != "ZVAB" || !abeIds.Contains(ListingNumber(o.Url))))
                db.PriceFinds.Add(new PriceFind
                {
                    ItemId = itemId, Source = name, IsNew = isNew, Name = Cut(o.Name, 300), Kr = (int)o.Price!.Value,
                    Original = o.Original, Url = o.Url, FoundAt = now,
                });
        }

        var ok = answers.Count(a => a.Result is not null);
        // Nothing answered (net down?) - try again tomorrow instead of in 30 days.
        item.PricesAt = ok == 0 ? now.AddDays(-29) : now;
        var used = answers.Where(a => !a.IsNew && a.Result is { Ambiguous: false })
            .SelectMany(a => Keep(a.Result!).Where(o => a.Name != "ZVAB" || !abeIds.Contains(ListingNumber(o.Url))))
            .Select(o => (int)o.Price!.Value).ToList();
        if (Summary.NormalOf(used) is { } normal) item.PriceHint = normal;
        await db.SaveChangesAsync(ct);
        return ok;
    }

    // The cheapest, the ones around the middle and the dearest - so both "normally 5 kr" and "50 kr on Amazon" are kept.
    internal static List<Offer> Keep(Listings r)
    {
        var priced = r.All.Where(o => o.Price is > 0).DistinctBy(o => (o.Url, o.Price)).OrderBy(o => o.Price).ToList();
        if (priced.Count <= KeepPerSource) return priced;
        var mid = priced.Count / 2;
        return priced.Take(4).Concat(priced.Skip(mid - 2).Take(4)).Concat(priced.TakeLast(4)).Distinct().ToList();
    }

    // ".../Mort-Terry-Pratchett-Corgi-Books/31992988332/bd" -> "31992988332"
    internal static string ListingNumber(string url) =>
        System.Text.RegularExpressions.Regex.Match(url, @"/(\d{6,})/b[di]") is { Success: true } m ? m.Groups[1].Value : "";

    private static int? Kr(decimal? v) => v is null ? null : (int)Math.Round(v.Value);
    private static string Cut(string s, int n) => s.Length <= n ? s : s[..n];

    // What the finds add up to - shared by the hunter and the pages.
    public static class Summary
    {
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
}

// Works through the register in the background: items that were never priced first, then the ones priced more than
// 30 days ago. A few items a minute - every site gets a handful of searches a minute, never a flood. Off with
// Lager__PriceHunter=false; Lager__PriceHuntPerMinute sets the pace (default 4 = ~5,700 items a day).
public sealed class PriceHunter(IServiceScopeFactory scopes, IDbContextFactory<LagerDb> dbf, IConfiguration cfg, ILogger<PriceHunter> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!cfg.GetValue("Lager:PriceHunter", true)) { log.LogInformation("Price hunter is off (Lager__PriceHunter=false)"); return; }
        var perMinute = Math.Clamp(cfg.GetValue("Lager:PriceHuntPerMinute", 4), 1, 30);
        try { await Task.Delay(TimeSpan.FromSeconds(30), stop); } catch (OperationCanceledException) { return; }

        while (!stop.IsCancellationRequested)
        {
            var started = DateTime.UtcNow;
            try
            {
                List<int> ids;
                await using (var db = await dbf.CreateDbContextAsync(stop))
                {
                    var stale = DateTime.UtcNow.AddDays(-30);
                    ids = await db.Items
                        .Where(i => PriceHunt.HuntedKinds.Contains(i.Kind) && i.Status != Statuses.Solgt && (i.Title != "" || i.Series != "")
                                    && (i.PricesAt == null || i.PricesAt < stale))
                        .OrderBy(i => i.PricesAt != null).ThenBy(i => i.PricesAt).ThenBy(i => i.Id)
                        .Select(i => i.Id).Take(perMinute).ToListAsync(stop);
                }
                foreach (var id in ids)
                {
                    using var scope = scopes.CreateScope();
                    var ok = await scope.ServiceProvider.GetRequiredService<PriceHunt>().HuntAsync(id, stop);
                    log.LogInformation("Priced item {Id}: {Ok} sources answered", id, ok);
                }
                if (ids.Count == 0) { await Task.Delay(TimeSpan.FromMinutes(10), stop); continue; }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { log.LogWarning(ex, "Price hunter round failed"); }

            var wait = TimeSpan.FromMinutes(1) - (DateTime.UtcNow - started);
            if (wait > TimeSpan.Zero)
                try { await Task.Delay(wait, stop); } catch (OperationCanceledException) { return; }
        }
    }
}
