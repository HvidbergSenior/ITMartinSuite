using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

// A celebration gallery (Galleries__N__CelebrationFolder) turns one folder of a
// library - typically a SmartFolders person folder - into a single timeline page
// (wwwroot/fejring.html): everything in capture order, grouped per year of the
// person's life, with texts from fejring.json in that same folder.
//
// Capture dates come from EXIF/QuickTime, then from a date in the file name. Old
// scanned album pages have neither and land in "undated" - the page shows them
// as their own section instead of guessing a year.
public static partial class Celebration
{
    public sealed record Item(string Name, string Kind, string Url, string? Thumb, DateTime? Date);

    public sealed record Timeline(DateTime Built, List<Item> Items);

    // Reading EXIF of ~1,000 files is a few seconds on the NAS; the folder only
    // changes when someone re-runs the person step, so keep the result a while.
    private static readonly Dictionary<string, Timeline> Cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    public static Timeline Build(string slug, string libraryRoot, string folderRel,
        Func<string, string> web, Func<string, string?> thumb)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(slug, out var hit) && DateTime.UtcNow - hit.Built < CacheTtl)
                return hit;
        }

        var folder = Path.GetFullPath(Path.Combine(libraryRoot, folderRel));
        var items = new List<Item>();

        if (System.IO.Directory.Exists(folder))
        {
            foreach (var f in System.IO.Directory.EnumerateFiles(folder))
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith('.') || name.Equals("fejring.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var kind = KindOf(Path.GetExtension(f).ToLowerInvariant());
                if (kind is null) continue;

                var date = kind is "img" or "vid" ? CaptureDate(f) : null;
                date ??= DateFromName(name);

                items.Add(new Item(name, kind, web(f), thumb(f), date));
            }
        }

        // Undated first (old albums are the earliest years), then by date, then name
        // so burst shots keep their order.
        items = items
            .OrderBy(i => i.Date.HasValue)
            .ThenBy(i => i.Date)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        items = DropDuplicates(items, folder);

        var timeline = new Timeline(DateTime.UtcNow, items);
        lock (Cache) Cache[slug] = timeline;
        return timeline;
    }

    // Person folders can hold byte-identical copies ("P1240031.jpg" + "P1240031_2.jpg")
    // left behind by the rotation fix. Show each picture once; only files of equal
    // size are hashed, so this stays cheap.
    private static List<Item> DropDuplicates(List<Item> items, string folder)
    {
        var bySize = items.GroupBy(i => new FileInfo(Path.Combine(folder, i.Name)).Length)
            .Where(g => g.Count() > 1);
        var drop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in bySize)
        {
            var seen = new HashSet<string>();
            foreach (var item in group.OrderBy(i => i.Name.Length).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(Path.Combine(folder, item.Name));
                var hash = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(stream));
                if (!seen.Add(hash)) drop.Add(item.Name);
            }
        }
        return drop.Count == 0 ? items : items.Where(i => !drop.Contains(i.Name)).ToList();
    }

    // Texts = fejring.json in the photo folder (written once, the library is mounted read-only)
    // with the edits made on the page on top (DataDir/celebrations/<slug>.json, writable).
    public static JsonObject Texts(string libraryRoot, string folderRel, string editsFile)
    {
        var baseTexts = ReadObject(Path.Combine(libraryRoot, folderRel, "fejring.json")) ?? new JsonObject();
        var edits = ReadObject(editsFile);
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

    private static readonly object EditLock = new();

    // One edit from the page. kind: caption (key = file name), year (key = "2014" or "undated"),
    // rotate (key = file name, value degrees), field (heroSub / intro / outro).
    public static void SaveEdit(string editsFile, string kind, string key, string? title, string? text, int? degrees)
    {
        lock (EditLock)
        {
            var o = ReadObject(editsFile) ?? new JsonObject();
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
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(editsFile)!);
            File.WriteAllText(editsFile, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
    }

    private static JsonObject? ReadObject(string file)
    {
        if (!File.Exists(file)) return null;
        try { return JsonNode.Parse(File.ReadAllText(file)) as JsonObject; }
        catch (JsonException) { return null; } // a typo must not take the page down
    }

    private static string? KindOf(string ext) => ext switch
    {
        ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".heic" or ".avif" => "img",
        ".mp4" or ".mov" or ".m4v" or ".webm" => "vid",
        ".mp3" or ".m4a" or ".aac" or ".wav" or ".ogg" or ".flac" => "aud",
        ".pdf" or ".doc" or ".docx" or ".txt" or ".md" => "doc",
        _ => null,
    };

    private static DateTime? CaptureDate(string path)
    {
        try
        {
            var dirs = ImageMetadataReader.ReadMetadata(path);

            var sub = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (sub is not null &&
                (sub.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var d) ||
                 sub.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out d)) &&
                Plausible(d))
                return d;

            var ifd0 = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 is not null && ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out d) && Plausible(d))
                return d;

            var qt = dirs.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
            if (qt is not null && qt.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out d) && Plausible(d))
                return d.ToLocalTime();
        }
        catch
        {
            // Unreadable metadata is common on old scans - fall back to the name.
        }
        return null;
    }

    // "20190802 121944", "2014-01-19 083441-1", "Resized 20180213 120525"
    [GeneratedRegex(@"(19|20)(\d{2})[-_]?(\d{2})[-_]?(\d{2})")]
    private static partial Regex NameDate();

    private static DateTime? DateFromName(string name)
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
    private static bool Plausible(DateTime d) =>
        d.Year >= 1990 && d <= DateTime.Now.AddDays(1) && !(d.Month == 1 && d.Day == 1 && d.Hour == 0 && d.Minute == 0);
}
