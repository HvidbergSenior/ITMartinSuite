using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Infrastructure.Persistence;

// EF Core + SQLite (lager.db). A short-lived DbContext per call - Blazor Server keeps components alive for minutes,
// so a long-lived context would hold stale rows.

public sealed class BoxRepository(IDbContextFactory<LagerDb> dbf) : IBoxRepository
{
    public async Task<Box> AddNextAsync(string place, CancellationToken ct)
    {
        // The highest number + 1 (the helpers pick numbers in any order, so not the newest box). Codes are zero-padded,
        // so the text order is the number order. Two at once: the unique code index refuses one - it takes the next.
        for (var attempt = 0; ; attempt++)
        {
            await using var db = await dbf.CreateDbContextAsync(ct);
            var last = await db.Boxes.OrderByDescending(b => b.Code).Select(b => b.Code).FirstOrDefaultAsync(ct);
            var box = new Box { Code = Box.NextCode(last), Place = place };
            db.Boxes.Add(box);
            try { await db.SaveChangesAsync(ct); return box; }
            catch (DbUpdateException) when (attempt < 5) { }
        }
    }

    public async Task<Box> GetOrAddAsync(string code, CancellationToken ct)
    {
        code = Box.CodeFor(BoxCodes.Number(code));
        for (var attempt = 0; ; attempt++)
        {
            await using var db = await dbf.CreateDbContextAsync(ct);
            if (await db.Boxes.FirstOrDefaultAsync(b => b.Code == code, ct) is { } found) return found;
            var box = new Box { Code = code };
            db.Boxes.Add(box);
            try { await db.SaveChangesAsync(ct); return box; }
            catch (DbUpdateException) when (attempt < 3) { }   // made at the same moment elsewhere - read it again
        }
    }

    public async Task<Box?> GetAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Boxes.FindAsync([id], ct);
    }

    public async Task<Box?> ByCodeAsync(string code, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Boxes.FirstOrDefaultAsync(b => b.Code == code, ct);
    }

    public async Task<List<Box>> RecentAsync(int count, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Boxes.OrderByDescending(b => b.Id).Take(count).ToListAsync(ct);
    }

    public async Task<List<BoxSummary>> SummariesAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var counts = await db.Items.Where(i => i.BoxId != null).GroupBy(i => i.BoxId)
            .Select(g => new { Id = g.Key!.Value, Lines = g.Count(), Units = g.Sum(i => i.Quantity) }).ToDictionaryAsync(x => x.Id, ct);
        return (await db.Boxes.OrderByDescending(b => b.Id).ToListAsync(ct))
            .Select(b => new BoxSummary(b.Id, b.Code, b.Place, counts.GetValueOrDefault(b.Id)?.Lines ?? 0, counts.GetValueOrDefault(b.Id)?.Units ?? 0, b.Note))
            .ToList();
    }

    public async Task<int> CountAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Boxes.CountAsync(ct);
    }

    public async Task SetPlaceAsync(int id, string place, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Boxes.Where(b => b.Id == id).ExecuteUpdateAsync(u => u.SetProperty(b => b.Place, place), ct);
    }

    public async Task SetNoteAsync(int id, string note, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Boxes.Where(b => b.Id == id).ExecuteUpdateAsync(u => u.SetProperty(b => b.Note, note), ct);
    }

    public async Task SetFundAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Boxes.Where(b => b.Id == id).ExecuteUpdateAsync(u => u.SetProperty(b => b.Fund, true), ct);
    }

    public async Task<List<Box>> FundBoxesAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Boxes.Where(b => b.Fund).OrderByDescending(b => b.Id).ToListAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Boxes.Where(b => b.Id == id).ExecuteDeleteAsync(ct);
    }
}

public sealed class ItemRepository(IDbContextFactory<LagerDb> dbf) : IItemRepository
{
    public async Task<Item?> GetAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Items.Include(i => i.Box).FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<List<Item>> InBoxAsync(int boxId, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Items.Where(i => i.BoxId == boxId).ToListAsync(ct);
    }

    // Every word must match somewhere (title, artist, series, number, year ...): "anders 1968" finds Anders And 1968.
    public async Task<List<Item>> SearchAsync(string text, string? kind, string? valuation, int max, CancellationToken ct, ItemScope? scope = null)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await Matching(db, text, kind, valuation, scope)
            .OrderBy(i => i.Series).ThenBy(i => i.Year).ThenBy(i => i.Number).ThenBy(i => i.Title).Take(max).ToListAsync(ct);
    }

    // How many match in all - the list stops at max (user 2026-10-31: "says 200 and 196 later").
    public async Task<int> CountAsync(string text, string? kind, string? valuation, CancellationToken ct, ItemScope? scope = null)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await Matching(db, text, kind, valuation, scope).CountAsync(ct);
    }

    public async Task<List<(Box Box, int Count)>> NewPerBoxAsync(DateTime sinceUtc, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var rows = await db.Items.Where(i => i.CreatedAt >= sinceUtc && i.BoxId != null)
            .GroupBy(i => i.BoxId).Select(g => new { BoxId = g.Key!.Value, Count = g.Count(), Last = g.Max(i => i.CreatedAt) }).ToListAsync(ct);
        var ids = rows.Select(r => r.BoxId).ToList();
        var boxes = await db.Boxes.Where(b => ids.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);
        return rows.Where(r => boxes.ContainsKey(r.BoxId)).OrderByDescending(r => r.Last).Select(r => (boxes[r.BoxId], r.Count)).ToList();
    }

    private static IQueryable<Item> Matching(LagerDb db, string text, string? kind, string? valuation, ItemScope? scope = null)
    {
        var q = db.Items.Include(i => i.Box).AsQueryable();
        if (scope?.SinceUtc is { } since) q = q.Where(i => i.CreatedAt >= since);
        if (scope?.BoxId is { } box) q = q.Where(i => i.BoxId == box);
        if (scope?.Category is { } cat) q = q.Where(i => i.Category == cat);
        if (scope?.Language is { } lang) q = q.Where(i => i.Language == lang);
        if (!string.IsNullOrEmpty(kind)) q = q.Where(i => i.Kind == kind);
        // Item.Valuation, as SQL.
        q = valuation switch
        {
            Valuations.InDeal => q.Where(i => i.Deal != ""),
            Valuations.Valued => q.Where(i => i.Deal == "" && i.PriceHint > 0),
            Valuations.Waiting => q.Where(i => i.Deal == "" && (i.PriceHint == null || i.PriceHint <= 0) && i.PricesAt == null),
            Valuations.NoPrice => q.Where(i => i.Deal == "" && (i.PriceHint == null || i.PriceHint <= 0) && i.PricesAt != null),
            _ => q,
        };
        foreach (var word in text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6))
        {
            var w = word;
            // A box number (K5001, k12) means that box; another word may also be in the box's name (user 2026-11-03:
            // "when searching I should be able to search for boxes" - fx "lystrup" for Session 2's box).
            if (BoxWord.IsMatch(w))
            {
                var code = BoxCodes.For(BoxCodes.Number(w));
                q = q.Where(i => i.Box != null && i.Box.Code == code);
            }
            else q = q.Where(i => i.Search.Contains(w) || (i.Box != null && i.Box.Note.ToLower().Contains(w)));
        }
        return q;
    }

    private static readonly System.Text.RegularExpressions.Regex BoxWord = new(@"^k\d{1,4}$");

    // Shop.ForSale / Shop.Listed as SQL: in stock or for sale, copies left, a title (+ sure, or an unsure blad in the Blade
    // deal). With the box: a test box = only for show (Box.IsTest).
    private static IQueryable<Item> ForSale(LagerDb db) =>
        db.Items.Include(i => i.Box).Where(i => (i.Status == Statuses.Lager || i.Status == Statuses.TilSalg) && i.Quantity > 0 && i.Title != "");

    private static readonly string BladeDeal = ShopDeals.Blade.Name;

    // The price the shop shows, as SQL: own price, else the suggested one, else the standard price for its kind when it is a
    // sure reading that has been searched (Shop.StandardKr, user 2026-11-02 "what about products without price?").
    private static readonly System.Linq.Expressions.Expression<Func<Item, decimal?>> ShopKr = BuildShopKr();

    private static System.Linq.Expressions.Expression<Func<Item, decimal?>> BuildShopKr()
    {
        var i = System.Linq.Expressions.Expression.Parameter(typeof(Item), "i");
        System.Linq.Expressions.Expression Prop(string name) => System.Linq.Expressions.Expression.Property(i, name);
        var none = System.Linq.Expressions.Expression.Constant(null, typeof(decimal?));
        var own = System.Linq.Expressions.Expression.Coalesce(Prop(nameof(Item.Price)), Prop(nameof(Item.PriceHint)));
        System.Linq.Expressions.Expression standard = none;
        foreach (var (kind, kr) in Shop.StandardKr)
            standard = System.Linq.Expressions.Expression.Condition(
                System.Linq.Expressions.Expression.Equal(Prop(nameof(Item.Kind)), System.Linq.Expressions.Expression.Constant(kind)),
                System.Linq.Expressions.Expression.Constant((decimal?)kr, typeof(decimal?)), standard);
        var canStandard = System.Linq.Expressions.Expression.AndAlso(
            System.Linq.Expressions.Expression.NotEqual(Prop(nameof(Item.PricesAt)), System.Linq.Expressions.Expression.Constant(null, typeof(DateTime?))),
            System.Linq.Expressions.Expression.GreaterThanOrEqual(Prop(nameof(Item.Confidence)), System.Linq.Expressions.Expression.Constant(Pricing.MinConfidence)));
        var hasOwn = System.Linq.Expressions.Expression.GreaterThan(own, System.Linq.Expressions.Expression.Constant(0m, typeof(decimal?)));
        var body = System.Linq.Expressions.Expression.Condition(hasOwn, own,
            System.Linq.Expressions.Expression.Condition(canStandard, standard, none));
        return System.Linq.Expressions.Expression.Lambda<Func<Item, decimal?>>(body, i);
    }

    // ShopKr compared with a number, as a Where.
    private static System.Linq.Expressions.Expression<Func<Item, bool>> Kr(Func<System.Linq.Expressions.Expression, System.Linq.Expressions.Expression, System.Linq.Expressions.BinaryExpression> op, decimal kr) =>
        System.Linq.Expressions.Expression.Lambda<Func<Item, bool>>(op(ShopKr.Body, System.Linq.Expressions.Expression.Constant((decimal?)kr, typeof(decimal?))), ShopKr.Parameters);

    private static readonly System.Linq.Expressions.Expression<Func<Item, bool>> HasKr = Kr(System.Linq.Expressions.Expression.GreaterThan, 0);
    private static readonly System.Linq.Expressions.Expression<Func<Item, bool>> NoKr =
        System.Linq.Expressions.Expression.Lambda<Func<Item, bool>>(System.Linq.Expressions.Expression.Not(HasKr.Body), HasKr.Parameters);

    // A price band: shop price (no deal) from kr up to under below.
    private static IQueryable<Item> Priced(IQueryable<Item> q, decimal from, decimal? below)
    {
        q = q.Where(i => i.Deal == "").Where(Kr(System.Linq.Expressions.Expression.GreaterThanOrEqual, from));
        return below is { } b ? q.Where(Kr(System.Linq.Expressions.Expression.LessThan, b)) : q;
    }

    private static readonly string PerPieceDeal = ShopDeals.PerPiece.Name;

    // What bogshoppen.dk shows (user 2026-11-02): not the 3 kr Anders And/Basserne bin, and of the Anders And-blade with their
    // own price only the dearest ShopDeals.MaxRareAndersAndOnPage.
    private static IQueryable<Item> OnPage(LagerDb db)
    {
        var rare = ForSale(db).Where(i => i.Kind == Kinds.AndersAnd && i.Deal == "" && (i.Price ?? i.PriceHint) > 0)
            .OrderByDescending(i => i.Price ?? i.PriceHint).ThenBy(i => i.Id).Take(ShopDeals.MaxRareAndersAndOnPage).Select(i => i.Id);
        // ... and not the unsure readings until a person has checked them (user 2026-11-02: "hide until checked").
        return ForSale(db).Where(i => i.Deal != PerPieceDeal && i.Confidence >= Pricing.MinConfidence
            && (i.Kind != Kinds.AndersAnd || i.Deal != "" || rare.Contains(i.Id)));
    }

    private static IQueryable<Item> Listed(LagerDb db) => OnPage(db);

    // Sure readings: everything but the Unsure band.
    private static IQueryable<Item> Sure(LagerDb db) => OnPage(db).Where(i => i.Confidence >= Pricing.MinConfidence || i.Deal == BladeDeal);

    // ShopDeals bands as SQL: Single = priced and in no deal, NoPrice = no price and in no deal, a deal name = that deal.
    private static IQueryable<Item> Listed(LagerDb db, string? band) => band switch
    {
        null => Listed(db),
        ShopDeals.Single => Sure(db).Where(i => i.Deal == "").Where(HasKr),
        ShopDeals.NoPrice => Sure(db).Where(i => i.Deal == "").Where(NoKr),
        ShopDeals.Unsure => OnPage(db).Where(i => i.Confidence < Pricing.MinConfidence && i.Deal == ""),
        _ when ShopDeals.PriceBand(band) is { } p => Priced(Sure(db), p.From, p.Below),
        _ => Listed(db).Where(i => i.Deal == band),
    };

    public async Task<Dictionary<string, int>> ListedPerDealAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        // ForSale, not OnPage: the 3 kr bin is off the page but its count makes the online packs.
        return await ForSale(db).Where(i => i.Deal != "").GroupBy(i => i.Deal).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task<List<Item>> AllListedAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await ForSale(db).ToListAsync(ct);
    }

    public async Task<(List<Item> Page, int Total)> ListedAsync(IReadOnlyCollection<string>? kinds, string? usage, string text, string sort, int skip, int take, CancellationToken ct,
        string? band = null, IReadOnlyCollection<string>? categories = null, string? language = null)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var q = band == ShopDeals.Rare ? await RareAsync(db, ct) : Listed(db, band);
        if (kinds is not null) q = q.Where(i => kinds.Contains(i.Kind));
        q = InCategory(q, categories, language);
        if (usage is not null) q = q.Where(i => i.Usage == usage);
        foreach (var word in text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6))
        {
            var w = word;
            q = q.Where(i => i.Search.Contains(w));
        }
        var total = await q.CountAsync(ct);
        // A-Å is Danish name order (NameOrder) - SQLite cannot do it, so in memory: the things for sale are about a thousand rows.
        if (sort == ShopSorts.Title)
            return ((await q.ToListAsync(ct)).Order(NameOrder.Instance).Skip(skip).Take(take).ToList(), total);
        // The price a buyer sees: a person's price, else the suggested shop price; things without one last.
        q = sort switch
        {
            ShopSorts.Cheapest => q.OrderBy(NoKr).ThenBy(ShopKr).ThenBy(i => i.Title),
            ShopSorts.Dearest => q.OrderBy(NoKr).ThenByDescending(ShopKr).ThenBy(i => i.Title),
            ShopSorts.Title => q.OrderBy(i => i.Title).ThenBy(i => i.Year).ThenBy(i => i.Number),
            _ => q.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id),
        };
        return (await q.Skip(skip).Take(take).ToListAsync(ct), total);
    }

    // ShopDeals.Rare: the things on the page with a mark (Shop.Badge needs the gold list, so it is worked out in memory -
    // about a thousand rows) as a query of their ids.
    private static async Task<IQueryable<Item>> RareAsync(LagerDb db, CancellationToken ct)
    {
        var gold = await db.GoldEntries.AsNoTracking().ToListAsync(ct);
        var ids = (await OnPage(db).AsNoTracking().ToListAsync(ct)).Where(i => Shop.Badge(gold, i) != "").Select(i => i.Id).ToList();
        return OnPage(db).Where(i => ids.Contains(i.Id));
    }

    // One or more main categories and/or one language (Item.Category / Item.Language, 2026-11-03: "Interested in books and
    // dvds... Only those products").
    private static IQueryable<Item> InCategory(IQueryable<Item> q, IReadOnlyCollection<string>? categories, string? language)
    {
        if (categories is { Count: > 0 }) q = q.Where(i => categories.Contains(i.Category));
        if (!string.IsNullOrEmpty(language)) q = q.Where(i => i.Language == language);
        return q;
    }

    public async Task<Dictionary<string, int>> ListedPerCategoryAsync(CancellationToken ct, string? band = null, string? language = null)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var q = InCategory(band == ShopDeals.Rare ? await RareAsync(db, ct) : Listed(db, band), null, language);
        return await q.GroupBy(i => i.Category).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task<Dictionary<string, int>> ListedPerKindAsync(CancellationToken ct, string? band = null)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var q = band == ShopDeals.Rare ? await RareAsync(db, ct) : Listed(db, band);
        return await q.GroupBy(i => i.Kind).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task<Dictionary<string, int>> ListedPerGroupAsync(IReadOnlyCollection<string>? kinds, CancellationToken ct, string? band = null,
        IReadOnlyCollection<string>? categories = null, string? language = null)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var q = band == ShopDeals.Rare ? await RareAsync(db, ct) : Listed(db, band);
        if (kinds is not null) q = q.Where(i => kinds.Contains(i.Kind));
        q = InCategory(q, categories, language);
        return await q.Select(i => i.Series != "" ? i.Series : i.Artist).Where(g => g != "")
            .GroupBy(g => g).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task<List<Item>> ByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Items.Where(i => ids.Contains(i.Id)).ToListAsync(ct);
    }

    public async Task<List<Item>> WithTitlesAsync(IReadOnlyCollection<string> titles, CancellationToken ct, IReadOnlyCollection<string>? barcodes = null)
    {
        var keys = titles.Select(Item.TitleKey).Where(t => t != "").ToList();
        var codes = (barcodes ?? []).Where(b => !string.IsNullOrWhiteSpace(b)).ToHashSet();
        if (keys.Count == 0 && codes.Count == 0) return [];
        await using var db = await dbf.CreateDbContextAsync(ct);
        // Compared in memory: the key drops punctuation, SQLite's lower() only folds A-Z, and a scanned title may be the
        // cover title plus a subtitle (Item.LooksLike has the final say).
        var candidates = await db.Items.Where(i => i.Status != Statuses.Solgt).Select(i => new { i.Id, i.Title, i.Barcode }).ToListAsync(ct);
        bool Near(string t)
        {
            var k = Item.TitleKey(t);
            return k != "" && keys.Any(x => x == k || (Math.Min(x.Length, k.Length) >= 8 && (x.StartsWith(k) || k.StartsWith(x))));
        }
        var ids = candidates.Where(c => Near(c.Title) || (c.Barcode != "" && codes.Contains(c.Barcode))).Select(c => c.Id).ToList();
        return await db.Items.Include(i => i.Box).Where(i => ids.Contains(i.Id)).ToListAsync(ct);
    }

    public async Task<List<Item>> FeaturedAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Items.Include(i => i.Box).Where(i => i.FeaturedAt != null && (i.Status == Statuses.Lager || i.Status == Statuses.TilSalg))
            .OrderByDescending(i => i.FeaturedAt).Take(max).ToListAsync(ct);
    }

    public async Task<int> UnitsAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Items.Where(i => i.Status != Statuses.Fund && i.Status != Statuses.IkkeKoebt).SumAsync(i => (int?)i.Quantity, ct) ?? 0;
    }

    public async Task AddAsync(IEnumerable<Item> items, CancellationToken ct)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        await using var db = await dbf.CreateDbContextAsync(ct);
        foreach (var i in list) i.Box = null;   // the box is already saved - only its id goes with the item
        db.Items.AddRange(list);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(IEnumerable<Item> items, CancellationToken ct)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        await using var db = await dbf.CreateDbContextAsync(ct);
        var boxes = list.Select(i => i.Box).ToList();
        foreach (var i in list) { i.Box = null; db.Items.Update(i); }   // only the box id is saved with an item
        await db.SaveChangesAsync(ct);
        for (var n = 0; n < list.Count; n++) list[n].Box = boxes[n];     // the caller keeps showing it
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Items.Where(i => i.Id == id).ExecuteDeleteAsync(ct);
        await db.PriceFinds.Where(f => f.ItemId == id).ExecuteDeleteAsync(ct);
        await db.PriceChecks.Where(c => c.ItemId == id).ExecuteDeleteAsync(ct);
    }

    public async Task<List<Item>> ByMagicRefsAsync(IReadOnlyCollection<string> refs, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Items.Where(i => refs.Contains(i.MagicRef)).ToListAsync(ct);
    }

    public async Task<List<Item>> OnAuctionAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Items.Where(i => i.AuctionItemId != "" && i.Status == Statuses.PaaAuktion).ToListAsync(ct);
    }

    public async Task<List<int>> NextToHuntAsync(string[] kinds, DateTime staleBefore, int count, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Items
            .Where(i => kinds.Contains(i.Kind) && i.Status != Statuses.Solgt && i.Status != Statuses.IkkeKoebt && (i.Title != "" || i.Series != "")
                        && (i.PricesAt == null || i.PricesAt < staleBefore))
            .OrderBy(i => i.PricesAt != null).ThenBy(i => i.PricesAt).ThenBy(i => i.Id)
            .Select(i => i.Id).Take(count).ToListAsync(ct);
    }

    public async Task<List<(Item Item, int TopKr)>> KolofonWantedAsync(int fromKr, int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var top = await db.PriceFinds.Where(f => !f.IsNew && !f.Similar)
            .GroupBy(f => f.ItemId).Select(g => new { ItemId = g.Key, Top = g.Max(f => f.Kr) })
            .Where(x => x.Top >= fromKr).ToDictionaryAsync(x => x.ItemId, x => x.Top, ct);
        var ids = top.Keys.ToList();
        var items = await db.Items.Include(i => i.Box)
            .Where(i => (ids.Contains(i.Id) || i.EditionHint == EditionHints.First)
                        && i.Kind == Kinds.Bog && i.KolofonPhoto == "" && i.Status != Statuses.Solgt)
            .ToListAsync(ct);
        return items.Select(i => (i, top.GetValueOrDefault(i.Id)))
            .OrderByDescending(x => x.i.EditionHint == EditionHints.First).ThenByDescending(x => x.Item2).Take(max).ToList();
    }

    public async Task<List<Item>> KolofonWaitingAsync(int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Items.Include(i => i.Box).Where(i => i.KolofonPhoto != "" && i.KolofonAt == null)
            .OrderBy(i => i.UpdatedAt).Take(max).ToListAsync(ct);
    }

    public async Task<List<Item>> AllAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Items.Include(i => i.Box).ToListAsync(ct);
    }
}


public sealed class PriceRepository(IDbContextFactory<LagerDb> dbf) : IPriceRepository
{
    public async Task<Dictionary<int, List<PriceFind>>> FindsForAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct)
    {
        if (itemIds.Count == 0) return [];
        await using var db = await dbf.CreateDbContextAsync(ct);
        return (await db.PriceFinds.Where(f => itemIds.Contains(f.ItemId)).ToListAsync(ct))
            .GroupBy(f => f.ItemId).ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task<(List<PriceFind> Finds, List<PriceCheck> Checks)> ForItemAsync(int itemId, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return (await db.PriceFinds.Where(f => f.ItemId == itemId).ToListAsync(ct),
            await db.PriceChecks.Where(c => c.ItemId == itemId).ToListAsync(ct));
    }

    public async Task ReplaceAsync(int itemId, IEnumerable<PriceCheck> checks, IEnumerable<PriceFind> finds, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.PriceChecks.Where(c => c.ItemId == itemId).ExecuteDeleteAsync(ct);
        await db.PriceFinds.Where(f => f.ItemId == itemId).ExecuteDeleteAsync(ct);
        db.PriceChecks.AddRange(checks);
        db.PriceFinds.AddRange(finds);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<Dictionary<int, PriceFind>> TopFindsAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct)
    {
        if (itemIds.Count == 0) return [];
        await using var db = await dbf.CreateDbContextAsync(ct);
        var finds = await db.PriceFinds.Where(f => itemIds.Contains(f.ItemId) && !f.Similar).ToListAsync(ct);
        return finds.GroupBy(f => f.ItemId).ToDictionary(g => g.Key, g => g.MaxBy(f => f.Kr)!);
    }
}

public sealed class BookCheckRepository(IDbContextFactory<LagerDb> dbf) : IBookCheckRepository
{
    public async Task<BookCheck?> GetAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.BookChecks.FindAsync([id], ct);
    }

    public async Task SaveAsync(IEnumerable<BookCheck> checks, CancellationToken ct)
    {
        var list = checks.ToList();
        if (list.Count == 0) return;
        await using var db = await dbf.CreateDbContextAsync(ct);
        foreach (var c in list)
            if (c.Id == 0) db.BookChecks.Add(c); else db.BookChecks.Update(c);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<BookCheck>> SearchAsync(string text, int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var q = db.BookChecks.AsQueryable();
        foreach (var w in text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6))
        {
            var word = w;
            q = q.Where(b => b.Search.Contains(word));
        }
        return await q.OrderByDescending(b => b.CheckedAt).Take(max).ToListAsync(ct);
    }

    public async Task<int> CountAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.BookChecks.CountAsync(ct);
    }

    public async Task SetBoxAsync(IReadOnlyCollection<int> ids, string boxCode, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.BookChecks.Where(c => ids.Contains(c.Id)).ExecuteUpdateAsync(u => u.SetProperty(c => c.BoxCode, boxCode), ct);
    }

    public async Task<List<BookCheck>> AllAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.BookChecks.ToListAsync(ct);
    }
}

public sealed class GoldRepository(IDbContextFactory<LagerDb> dbf) : IGoldRepository
{
    public async Task ReplaceAsync(string category, IReadOnlyList<GoldEntry> entries, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.GoldEntries.RemoveRange(db.GoldEntries.Where(e => e.Category == category));
        db.GoldEntries.AddRange(entries);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<GoldEntry>> AllAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.GoldEntries.OrderBy(e => e.Category).ThenByDescending(e => e.Kr).ToListAsync(ct);
    }
}
