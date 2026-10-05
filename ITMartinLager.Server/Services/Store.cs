using ITMartinLager.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Server.Services;

// Boxes and items: numbering, saving a read pile, and search. Kept out of the pages so it can be tested.
public static class Store
{
    public static string CodeFor(int n) => $"K{n:D4}";

    public static async Task<Box> NewBoxAsync(LagerDb db, string place)
    {
        var last = await db.Boxes.OrderByDescending(b => b.Id).Select(b => b.Code).FirstOrDefaultAsync();
        var n = last is not null && int.TryParse(last.TrimStart('K'), out var x) ? x + 1 : 1;
        var box = new Box { Code = CodeFor(n), Place = place.Trim() };
        db.Boxes.Add(box);
        await db.SaveChangesAsync();
        return box;
    }

    // Same thing in the same box and the same condition = one row with a higher quantity, so 7 copies of
    // Anders And 12/1968 stay one line however many photos they turned up in.
    public static async Task<(int added, int merged)> SaveAsync(LagerDb db, int boxId, int? photoId, IEnumerable<Found> found)
    {
        int added = 0, merged = 0;
        var inBox = await db.Items.Where(i => i.BoxId == boxId).ToListAsync();
        foreach (var f in found)
        {
            var same = inBox.FirstOrDefault(i => Same(i, f));
            if (same is not null)
            {
                same.Quantity += f.Quantity;
                same.Touch();
                merged++;
                continue;
            }
            var item = new Item
            {
                Kind = f.Kind, Title = f.Title, Artist = f.Artist, Series = f.Series, Number = f.Number, Year = f.Year,
                Platform = f.Platform, Condition = f.Condition, Quantity = f.Quantity, Confidence = f.Confidence,
                Note = f.Note, Interest = f.Interest, BoxId = boxId, PhotoId = photoId,
            };
            item.Touch();
            db.Items.Add(item);
            inBox.Add(item);
            added++;
        }
        await db.SaveChangesAsync();
        return (added, merged);
    }

    internal static bool Same(Item i, Found f) =>
        i.Kind == f.Kind && i.Condition == f.Condition && i.Year == f.Year
        && Eq(i.Title, f.Title) && Eq(i.Artist, f.Artist) && Eq(i.Series, f.Series) && Eq(i.Number, f.Number) && Eq(i.Platform, f.Platform);

    private static bool Eq(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    // Every word must match somewhere (title, artist, series, number, year ...): "anders 1968" finds Anders And 1968.
    public static IQueryable<Item> Search(IQueryable<Item> q, string text, string? kind)
    {
        if (!string.IsNullOrEmpty(kind)) q = q.Where(i => i.Kind == kind);
        foreach (var word in text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6))
        {
            var w = word;
            q = q.Where(i => i.Search.Contains(w));
        }
        return q;
    }
}
