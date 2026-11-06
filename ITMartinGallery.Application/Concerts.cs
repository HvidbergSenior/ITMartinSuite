using System.Text.Json;
using System.Text.Json.Nodes;
using ITMartinGallery.Domain;
using Item = ITMartinGallery.Domain.ConcertRules.Item;

namespace ITMartinGallery.Application;

public sealed record ConcertCard(string Key, string Title, string? Date, string? Venue, string? Cover, int Photos, int Videos);
public sealed record ConcertSeries(string Slug, string Name, JsonObject Texts, IEnumerable<ConcertCard> Shows);
public sealed record ConcertTile(string Name, string Kind, string Url, string? Thumb, string? Date, string? Caption);
public sealed record ConcertShow(string Key, string Title, string? Date, string? Venue, JsonObject Texts, IEnumerable<ConcertTile> Items);

// The stage pages of a concert gallery (wwwroot/koncert.html) - see ConcertRules.
public sealed class Concerts(ILibraryDisk disk, ICaptureDates captureDates, LibraryLinks links)
{
    private readonly Dictionary<string, (DateTime Built, List<Item> Items)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);   // a new concert shows within half an hour

    public ConcertSeries Series(Gallery g)
    {
        var cards = new List<(DateTime? Date, ConcertCard Card)>();
        if (disk.DirectoryExists(g.Path))
            foreach (var dir in disk.Directories(g.Path))
            {
                var key = Path.GetFileName(dir);
                if (!ConcertRules.IsShowFolder(key)) continue;
                var items = Items(g, key);
                if (items.Count == 0) continue;
                var t = Texts(Path.Combine(dir, "koncert.json"));
                var date = ConcertRules.ShowDate(t["date"]?.ToString(), items);
                var cover = t["cover"]?.ToString() is { Length: > 0 } c ? items.FirstOrDefault(i => i.Name == c) : null;
                cover ??= items.FirstOrDefault(i => i.Kind == "img") ?? items[0];
                cards.Add((date, new ConcertCard(key, t["title"]?.ToString() ?? ConcertRules.NiceName(key), date?.ToString("yyyy-MM-dd"),
                    t["venue"]?.ToString(), cover.Thumb, items.Count(i => i.Kind == "img"), items.Count(i => i.Kind == "vid"))));
            }
        return new ConcertSeries(g.Slug, g.Name, Texts(Path.Combine(g.Path, "koncerter.json")),
            cards.OrderByDescending(c => c.Date ?? DateTime.MinValue).Select(c => c.Card).ToList());
    }

    public ConcertShow? Show(Gallery g, string key)
    {
        if (!ConcertRules.IsValidKey(key)) return null;
        var dir = Path.GetFullPath(Path.Combine(g.Path, key));
        if (!LibraryPaths.IsSafe(dir, g.Path) || !disk.DirectoryExists(dir)) return null;
        var t = Texts(Path.Combine(dir, "koncert.json"));
        var order = (t["order"] as JsonArray)?.Select(x => x?.ToString() ?? "").Where(x => x.Length > 0).ToList() ?? [];
        var items = ConcertRules.Ordered(Items(g, key), order);
        var captions = t["captions"] as JsonObject;
        var date = ConcertRules.ShowDate(t["date"]?.ToString(), items);
        return new ConcertShow(key, t["title"]?.ToString() ?? ConcertRules.NiceName(key), date?.ToString("yyyy-MM-dd"), t["venue"]?.ToString(), t,
            items.Select(i => new ConcertTile(i.Name, i.Kind, i.Url, i.Thumb, i.Date?.ToString("yyyy-MM-ddTHH:mm:ss"), captions?[i.Name]?.ToString())).ToList());
    }

    private List<Item> Items(Gallery g, string key)
    {
        var cacheKey = g.Slug + "|" + key;
        lock (_cache)
            if (_cache.TryGetValue(cacheKey, out var hit) && DateTime.UtcNow - hit.Built < CacheTtl) return hit.Items;

        var root = Path.GetFullPath(Path.Combine(g.Path, key));
        var items = new List<Item>();
        void Walk(string dir)
        {
            foreach (var f in disk.Files(dir))
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith('.')) continue;
                var kind = MediaTypes.CelebrationKind(Path.GetExtension(f).ToLowerInvariant());
                if (kind is not ("img" or "vid")) continue;
                items.Add(new Item(name, kind, links.Web(f, g.Path, g.Slug),
                    links.GalleriThumb(f, g.Path, g.Slug) ?? links.Thumb(f, g.Path, g.Slug), captureDates.Read(f)));
            }
            foreach (var d in disk.Directories(dir))
                if (!ConcertRules.IsHiddenFolder(Path.GetFileName(d))) Walk(d);
        }
        if (disk.DirectoryExists(root)) Walk(root);
        var ordered = ConcertRules.InPageOrder(items);
        lock (_cache) _cache[cacheKey] = (DateTime.UtcNow, ordered);
        return ordered;
    }

    private JsonObject Texts(string file)
    {
        if (disk.ReadText(file) is not { } text) return new JsonObject();
        try { return JsonNode.Parse(text) as JsonObject ?? new JsonObject(); }
        catch (JsonException) { return new JsonObject(); }   // a typo must not take the page down
    }
}
