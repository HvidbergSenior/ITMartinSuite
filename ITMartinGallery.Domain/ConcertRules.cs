using System.Globalization;
using System.Text.RegularExpressions;

namespace ITMartinGallery.Domain;

// KoncertKlanen (user 2026-11-06): "call it KoncertKlanen and this one is GuldIMund. Later it could be Northside2027 etc"
// + "Make a wauv like the birthday ones". A concert gallery (Galleries__N__Concerts=true) shows one stage page per
// concert: every direct sub-folder of the library is one concert (a FileSorter library of its own), newest first.
// Texts live in koncert.json in the concert folder (title, date, venue, text, captions) - written once, like fejring.json.
public static partial class ConcertRules
{
    public sealed record Item(string Name, string Kind, string Url, string? Thumb, DateTime? Date);

    /// <summary>Folders inside a concert that are never shown (FileSorter's own and its quarantine folders).</summary>
    public static readonly HashSet<string> Hidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "_Galleri", "thumbnails", "Review", "Dubletter", "SmåAlbummer", "Afspilningsfejl", "RotationUkendt",
        "SmartFolders", "DeleteCandidates", "LivePhotos", "Screenshots", "Skærmbilleder",
    };

    public static bool IsHiddenFolder(string name) => FolderNames.IsSystemFolder(name) || Hidden.Contains(name) || name.StartsWith('_');

    public static bool IsShowFolder(string name) => !IsHiddenFolder(name);

    /// <summary>"GuldIMund" -> "Guld I Mund", "Northside2027" -> "Northside 2027" - only when koncert.json has no title.</summary>
    public static string NiceName(string folder)
    {
        var s = Words().Replace(folder.Replace('_', ' ').Replace('-', ' '), " ");
        return MultiSpace().Replace(s, " ").Trim();
    }

    /// <summary>A safe concert key = one plain folder name (no slashes, no "..").</summary>
    public static bool IsValidKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key.Length <= 80 && !key.Contains('/') && !key.Contains('\\') && !key.Contains("..") && IsShowFolder(key);

    /// <summary>The order on the page: by time taken, undated last, then by name.</summary>
    public static List<Item> InPageOrder(IEnumerable<Item> items) =>
        items.OrderBy(i => i.Date ?? DateTime.MaxValue).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>koncert.json "order": the named files first, in that order (pictures sent via a messenger have no date);
    /// the rest keep the time-taken order after them.</summary>
    public static List<Item> Ordered(List<Item> items, IReadOnlyList<string> order)
    {
        if (order.Count == 0) return items;
        var at = order.Select((n, i) => (n, i)).GroupBy(x => x.n, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
        return items.Select((it, i) => (it, i)).OrderBy(x => at.TryGetValue(x.it.Name, out var k) ? k : order.Count + x.i).Select(x => x.it).ToList();
    }

    /// <summary>koncert.json "date" ("2026-11-05"), else the day of the first dated item.</summary>
    public static DateTime? ShowDate(string? date, IEnumerable<Item> items)
    {
        if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d.Date;
        var dated = items.Where(i => i.Date is not null).Select(i => i.Date!.Value).ToList();
        return dated.Count == 0 ? null : dated.Min().Date;
    }

    [GeneratedRegex(@"(?<=[a-zæøå])(?=[A-ZÆØÅ])|(?<=[A-ZÆØÅ])(?=[A-ZÆØÅ][a-zæøå])|(?<=[A-Za-zÆØÅæøå])(?=\d)")]
    private static partial Regex Words();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpace();
}
