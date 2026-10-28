using System.Text.Json;
using System.Text.Json.Nodes;
using ITMartinGallery.Domain;
using Item = ITMartinGallery.Domain.CelebrationRules.Item;
using Timeline = ITMartinGallery.Domain.CelebrationRules.Timeline;

namespace ITMartinGallery.Application;

public sealed record CelebrationTile(string Name, string Kind, string Url, string? Thumb, string? Date, string? Who);
public sealed record CelebrationPage(string Slug, string Name, string? BirthDate, JsonObject Texts, bool CanEdit, IEnumerable<CelebrationTile> Items);

// The birthday page (wwwroot/fejring.html) of a celebration gallery - see CelebrationRules.
public sealed class Celebrations(ILibraryDisk disk, ICaptureDates captureDates, ICelebrationEdits edits, LibraryLinks links)
{
    // Reading EXIF of ~1,000 files is a few seconds on the NAS; the folder only
    // changes when someone re-runs the person step, so keep the result a while.
    // Warmed at start (Program.cs); a deploy/restart refreshes it, so it can live long.
    private readonly Dictionary<string, Timeline> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(2);
    private readonly object _editLock = new();

    public CelebrationPage? Page(Gallery g)
    {
        var folder = Path.GetFullPath(Path.Combine(g.Path, g.CelebrationFolder!));
        if (!LibraryPaths.IsSafe(folder, g.Path)) return null;

        var timeline = Build(g);
        var texts = Texts(g);
        var shown = CelebrationRules.Shown(timeline.Items, texts);
        return new CelebrationPage(g.Slug, g.CelebrationName ?? g.Name, g.BirthDate, texts, !string.IsNullOrEmpty(g.EditPin),
            shown.Select(i => new CelebrationTile(i.Name, i.Kind, i.Url, i.Thumb, i.Date?.ToString("yyyy-MM-ddTHH:mm:ss"), i.Who)));
    }

    // The page's theme, set before the password screen so a themed page never shows the default look first.
    public string? Theme(Gallery g) => Texts(g)["theme"]?.ToString();

    // Texts = fejring.json in the photo folder (written once, the library is mounted read-only)
    // with the edits made on the page on top.
    public JsonObject Texts(Gallery g) =>
        CelebrationRules.Merge(ReadObject(Path.Combine(g.Path, g.CelebrationFolder!, "fejring.json")), edits.Read(g.Slug));

    // false = unknown kind of edit.
    public bool SaveEdit(Gallery g, string kind, string key, string? title, string? text, int? degrees)
    {
        lock (_editLock)
        {
            var o = edits.Read(g.Slug) ?? new JsonObject();
            try { CelebrationRules.ApplyEdit(o, kind, key, title, text, degrees); }
            catch (ArgumentException) { return false; }
            edits.Write(g.Slug, o);
            return true;
        }
    }

    public Timeline Build(Gallery g)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(g.Slug, out var hit) && DateTime.UtcNow - hit.Built < CacheTtl)
                return hit;
        }

        string Web(string f) => links.Web(f, g.Path, g.Slug);
        string? Thumb(string f) => links.GalleriThumb(f, g.Path, g.Slug) ?? links.Thumb(f, g.Path, g.Slug);

        var folder = Path.GetFullPath(Path.Combine(g.Path, g.CelebrationFolder!));
        var items = new List<Item>();

        if (disk.DirectoryExists(folder))
        {
            foreach (var f in disk.Files(folder))
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith('.') || name.Equals("fejring.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var kind = MediaTypes.CelebrationKind(Path.GetExtension(f).ToLowerInvariant());
                if (kind is null) continue;

                var date = kind is "img" or "vid" ? captureDates.Read(f) : null;
                date ??= CelebrationRules.DateFromName(name);

                items.Add(new Item(name, kind, Web(f), Thumb(f), date));
            }
        }

        items.AddRange(Extras(g.Path, folder, items, Web, Thumb, g.Born));
        items = DropDuplicates(CelebrationRules.InPageOrder(items), folder);

        var timeline = new Timeline(DateTime.UtcNow, items);
        lock (_cache) _cache[g.Slug] = timeline;
        return timeline;
    }

    // A few pictures of the people around the birthday person (siblings, the dog …), from their own
    // person folders: fejring.json "extraFrom": { "SmartFolders/People/Fie": { "count": 3, "caption": "Fie 🐶" } }.
    // Only dated pictures from the person's lifetime, none that are already on the page, spread evenly over the years.
    // Name = path relative to the page's folder ("../Fie/x.jpg"), so captions/rotation work as for the rest.
    private IEnumerable<Item> Extras(string libraryRoot, string folder, List<Item> own,
        Func<string, string> web, Func<string, string?> thumb, DateTime? birth)
    {
        if (ReadObject(Path.Combine(folder, "fejring.json"))?["extraFrom"] is not JsonObject from) yield break;
        var root = Path.GetFullPath(libraryRoot);
        var ownKeys = own.Select(i => (i.Name, disk.Length(Path.Combine(folder, i.Name)))).ToHashSet();
        var start = birth ?? DateTime.MinValue;
        foreach (var (rel, cfg) in from)
        {
            var dir = Path.GetFullPath(Path.Combine(root, rel));
            if (!LibraryPaths.IsSafe(dir, root) || !disk.DirectoryExists(dir)) continue;
            var count = Math.Clamp(cfg?["count"]?.GetValue<int>() ?? 3, 0, 12);   // 0 = only the "files" listed
            var who = cfg?["caption"]?.GetValue<string>();
            var candidates = new List<(string File, DateTime Date)>();
            foreach (var f in disk.Files(dir))
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith('.') || MediaTypes.CelebrationKind(Path.GetExtension(f).ToLowerInvariant()) != "img") continue;
                if (ownKeys.Contains((name, disk.Length(f)))) continue;   // same picture as one already on the page
                var date = captureDates.Read(f) ?? CelebrationRules.DateFromName(name);
                if (date is { } d && d >= start && d <= DateTime.Now) candidates.Add((f, d));
            }
            candidates.Sort((a, b) => a.Date.CompareTo(b.Date));
            // "files": pictures that must be on the page (e.g. Onkel Martin's least flattering one) - even undated.
            var must = (cfg?["files"] as JsonArray)?.Select(x => x?.GetValue<string>() ?? "")
                .Select(n => Path.GetFullPath(Path.Combine(dir, n)))
                .Where(f => LibraryPaths.IsSafe(f, dir) && disk.FileExists(f)).ToList() ?? [];
            foreach (var f in must)
                yield return new Item(Path.GetRelativePath(folder, f).Replace('\\', '/'), "img", web(f), thumb(f),
                    captureDates.Read(f) ?? CelebrationRules.DateFromName(Path.GetFileName(f)), who);
            candidates.RemoveAll(c => must.Contains(c.File));
            // "skip": pictures the person folder has by mistake (e.g. Mathias and Mona in Malene's folder)
            var skip = (cfg?["skip"] as JsonArray)?.Select(x => x?.GetValue<string>() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            candidates.RemoveAll(c => skip.Contains(Path.GetFileName(c.File)));
            if (count == 0) continue;
            foreach (var (f, d) in CelebrationRules.PickEvenly(candidates, count))
                yield return new Item(Path.GetRelativePath(folder, f).Replace('\\', '/'), "img", web(f), thumb(f), d, who);
        }
    }

    // Person folders can hold byte-identical copies ("P1240031.jpg" + "P1240031_2.jpg")
    // left behind by the rotation fix. Show each picture once; only files of equal
    // size are hashed, so this stays cheap.
    private List<Item> DropDuplicates(List<Item> items, string folder)
    {
        var bySize = items.GroupBy(i => disk.Length(Path.Combine(folder, i.Name)))
            .Where(g => g.Count() > 1);
        var drop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in bySize)
        {
            var seen = new HashSet<string>();
            foreach (var item in group.OrderBy(i => i.Name.Length).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
                if (!seen.Add(disk.Md5(Path.Combine(folder, item.Name)))) drop.Add(item.Name);
        }
        return drop.Count == 0 ? items : items.Where(i => !drop.Contains(i.Name)).ToList();
    }

    private JsonObject? ReadObject(string file)
    {
        if (disk.ReadText(file) is not { } text) return null;
        try { return JsonNode.Parse(text) as JsonObject; }
        catch (JsonException) { return null; } // a typo must not take the page down
    }
}
