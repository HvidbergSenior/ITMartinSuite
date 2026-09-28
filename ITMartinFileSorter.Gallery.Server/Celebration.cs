using System.Globalization;
using System.Text.Json;
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

        var timeline = new Timeline(DateTime.UtcNow, items);
        lock (Cache) Cache[slug] = timeline;
        return timeline;
    }

    public static JsonElement? Texts(string libraryRoot, string folderRel)
    {
        var file = Path.Combine(libraryRoot, folderRel, "fejring.json");
        if (!File.Exists(file)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            // A typo while editing the texts must not take the page down.
            return null;
        }
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
