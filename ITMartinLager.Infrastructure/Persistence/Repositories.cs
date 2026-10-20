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
            .Select(b => new BoxSummary(b.Id, b.Code, b.Place, counts.GetValueOrDefault(b.Id)?.Lines ?? 0, counts.GetValueOrDefault(b.Id)?.Units ?? 0))
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
    public async Task<List<Item>> SearchAsync(string text, string? kind, string? valuation, int max, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var q = db.Items.Include(i => i.Box).AsQueryable();
        if (!string.IsNullOrEmpty(kind)) q = q.Where(i => i.Kind == kind);
        // Item.Valuation, as SQL.
        q = valuation switch
        {
            Valuations.Valued => q.Where(i => i.PriceHint > 0),
            Valuations.Waiting => q.Where(i => (i.PriceHint == null || i.PriceHint <= 0) && i.PricesAt == null),
            Valuations.NoPrice => q.Where(i => (i.PriceHint == null || i.PriceHint <= 0) && i.PricesAt != null),
            _ => q,
        };
        foreach (var word in text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6))
        {
            var w = word;
            q = q.Where(i => i.Search.Contains(w));
        }
        return await q.OrderBy(i => i.Series).ThenBy(i => i.Year).ThenBy(i => i.Number).ThenBy(i => i.Title).Take(max).ToListAsync(ct);
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
        return await db.Items.Where(i => i.FeaturedAt != null && (i.Status == Statuses.Lager || i.Status == Statuses.TilSalg))
            .OrderByDescending(i => i.FeaturedAt).Take(max).ToListAsync(ct);
    }

    public async Task<int> UnitsAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Items.SumAsync(i => (int?)i.Quantity, ct) ?? 0;
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
            .Where(i => kinds.Contains(i.Kind) && i.Status != Statuses.Solgt && (i.Title != "" || i.Series != "")
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
