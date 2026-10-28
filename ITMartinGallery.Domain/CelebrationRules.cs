using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ITMartinGallery.Domain;

// A celebration gallery (Galleries__N__CelebrationFolder) turns one folder of a
// library - typically a SmartFolders person folder - into a single timeline page
// (wwwroot/fejring.html): everything in capture order, grouped per year of the
// person's life, with texts from fejring.json in that same folder.
//
// Capture dates come from EXIF/QuickTime, then from a date in the file name. Old
// scanned album pages have neither and land in "undated" - the page shows them
// as their own section instead of guessing a year.
public static partial class CelebrationRules
{
    // Who = default caption for a picture borrowed from another person's folder ("extraFrom" in fejring.json).
    public sealed record Item(string Name, string Kind, string Url, string? Thumb, DateTime? Date, string? Who = null);

    public sealed record Timeline(DateTime Built, List<Item> Items);

    // Undated first (old albums are the earliest years), then by date, then name
    // so burst shots keep their order.
    public static List<Item> InPageOrder(IEnumerable<Item> items) => items
        .OrderBy(i => i.Date.HasValue)
        .ThenBy(i => i.Date)
        .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    // fejring.json "fromYear": dated pictures before that year are left out (the old albums stay).
    // fejring.json "skip": pictures in the page's own folder that should not be shown (wrong person, disliked photo).
    public static List<Item> Shown(IEnumerable<Item> items, JsonObject texts)
    {
        var fromYear = texts["fromYear"]?.GetValue<int>() ?? 0;
        var skip = (texts["skip"] as JsonArray)?.Select(x => x?.ToString() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        return items.Where(i => (i.Date is null || i.Date.Value.Year >= fromYear) && !skip.Contains(i.Name)).ToList();
    }

    // count pictures spread evenly over the (date-sorted) candidates.
    public static List<T> PickEvenly<T>(List<T> candidates, int count) =>
        candidates.Count <= count
            ? candidates
            : Enumerable.Range(0, count).Select(i => candidates[(int)((i + 0.5) * candidates.Count / count)]).ToList();

    // Texts = fejring.json in the photo folder with the edits made on the page on top.
    public static JsonObject Merge(JsonObject? baseTexts, JsonObject? edits)
    {
        baseTexts ??= new JsonObject();
        if (edits is null) return baseTexts;
        foreach (var (key, value) in edits)
        {
            // "years", "captions", "rotate" merge per entry; plain fields replace.
            if (value is JsonObject eo && baseTexts[key] is JsonObject bo)
                foreach (var (k, v) in eo) bo[k] = v?.DeepClone();
            else
                baseTexts[key] = value?.DeepClone();
        }
        return baseTexts;
    }

    // A key longer than this, or blank, is not an edit.
    public static bool IsValidKey(string? key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 200;

    // One edit from the page. kind: caption (key = file name), year (key = "2014" or "undated"),
    // rotate (key = file name, value degrees), field (heroSub / intro / outro). Unknown kind = ArgumentException.
    public static void ApplyEdit(JsonObject o, string kind, string key, string? title, string? text, int? degrees)
    {
        JsonObject Section(string name) => o[name] as JsonObject ?? (JsonObject)(o[name] = new JsonObject());
        string Clean(string? s) => (s ?? "").Trim() is var t && t.Length > 600 ? t[..600] : (s ?? "").Trim();
        switch (kind)
        {
            case "caption": Section("captions")[key] = Clean(text); break;
            case "rotate": Section("rotate")[key] = ((degrees ?? 0) % 360 + 360) % 360; break;
            case "year":
                var target = key == "undated" ? (JsonObject)(o["undated"] ??= new JsonObject()) : (JsonObject)(Section("years")[key] ??= new JsonObject());
                target["title"] = Clean(title);
                target["text"] = Clean(text);
                break;
            case "field" when key is "heroSub" or "intro" or "outro": o[key] = Clean(text); break;
            default: throw new ArgumentException("unknown edit");
        }
    }

    // "20190802 121944", "2014-01-19 083441-1", "Resized 20180213 120525"
    [GeneratedRegex(@"(19|20)(\d{2})[-_]?(\d{2})[-_]?(\d{2})")]
    private static partial Regex NameDate();

    public static DateTime? DateFromName(string name)
    {
        var m = NameDate().Match(name);
        if (!m.Success) return null;
        return DateTime.TryParseExact(m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value + m.Groups[4].Value,
            "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && Plausible(d)
            ? d
            : null;
    }

    // Cameras with a flat clock battery write 1970/2000-01-01; a date like that
    // would put a teenage photo into the year of birth.
    public static bool Plausible(DateTime d) =>
        d.Year >= 1990 && d <= DateTime.Now.AddDays(1) && !(d.Month == 1 && d.Day == 1 && d.Hour == 0 && d.Minute == 0);
}
