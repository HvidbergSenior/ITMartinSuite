using System.Text.RegularExpressions;

namespace ITMartinGallery.Domain;

// A tile on a view-only front page.
public sealed record RootTile(string Name, string Rel, string Icon, int Row);

// One folder card in /api/browse.
public record FolderEntry(string name, string relPath, string? cover, int row = 99, string? icon = null);

// How the folders FileSorter/QuickSort writes are named, hidden, ordered and shown.
public static class FolderNames
{
    // Loose photos straight in a folder that also has subfolders (undated photos next to the year folders) are shown as
    // their own virtual folder after the real ones - user 2026-10-27: Annette's 88 undated photos were invisible.
    // Same name as the static export's page for them.
    public const string LooseFolderName = "Ukendt dato";

    // Root-level category folders QuickSort/2/3 create that hold implementation
    // detail, redundant companion content, or content already surfaced through
    // the "Samlinger" (Collections) row - not something a non-technical viewer
    // should browse directly. Hidden only at the library root, never inside a
    // real content folder (so a legitimately-named subfolder deeper in someone's
    // own photos is never affected). SmartFolders' content (Home/Outside/People/
    // Yearbook) is synced into collections.json instead, so it shows as grouped
    // cards up top rather than requiring a click into a raw folder.
    // Always hidden, for every tenant - internal/generated or needs a human, not
    // something any customer should browse directly.
    public static readonly HashSet<string> RootFoldersAlwaysHidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "_Galleri", "DeleteCandidates", "LivePhotos", "SmartFolders",
        // Videos LibraryPolishService couldn't confirm are playable (see
        // LibraryPolishService.UnplayableFolderName) - a human needs to look at
        // these, not a customer browsing their gallery.
        "Afspilningsfejl",
        // Real content, but a raw "here are your undated files"/"here are your
        // duplicates" folder reads as clutter/confusing rather than something
        // worth showing - not something a non-technical viewer needs to browse
        // directly. Musik (Audio) is real, wanted content though - shown as its
        // own category, just secondary to Billeder/Videoer.
        "Undated", "Duplicates",
        // Danish-named equivalents (LibraryExportService's actual output names)
        // plus "Review", the single consolidated folder new sorts use in place
        // of separate SlettesKandidater/Duplikater/Undated folders (2026-09-06).
        "SlettesKandidater", "Duplikater", "Review",
    };

    // Opt-in per tenant (Galleries__N__CoreCategoriesOnly) - for a showcase/demo
    // gallery where only the four core content categories should ever be
    // visible, regardless of whatever else lands at the library root (Undated,
    // Screenshots, DeleteCandidates, future categories, etc). Every existing
    // real tenant defaults to false and is unaffected - this is an allowlist,
    // not a replacement for the blocklist above.
    public static readonly HashSet<string> CoreCategoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Images", "Videos", "Documents", "Musik",
    };

    // The root of a view-only gallery, row by row, as the owner laid it out on
    // 2026-09-11: photos and videos first, then the people, then the three
    // derived sets, and documents last on their own. Relative paths are what
    // LibraryExportService and SmartFoldersService actually write.
    public static readonly RootTile[] ViewOnlyRootFolders =
    [
        new("Billeder", "Billeder", "🖼️", 0),
        new("Videoer", "Videoer", "🎬", 0),
        new("Musik", "Musik", "🎵", 0),
        new("Personer", "SmartFolders/People", "👤", 1),
        new("Årbog", "SmartFolders/Yearbook", "📚", 2),
        new("Rejser", "SmartFolders/Trips", "✈️", 2),
        new("Traditioner og mærkedage", "SmartFolders/Traditioner", "🎄", 2),
        new("Dokumenter", "Dokumenter", "📄", 3),
    ];

    // SmartFolders subfolders that are either listed above or are generated
    // machinery (BedsteBillede feeds collections.json, UkendtePersoner is
    // unreviewed clusters, Home/Outside is a coarse split). Anything else
    // sitting directly under SmartFolders is a hand-curated album - first one
    // "Trøstebog Evia" (2026-09-15), a source folder the family wanted kept
    // together as its own view next to the derived sets - and gets its own card
    // on the view-only front page, named exactly as the folder on disk.
    public static readonly HashSet<string> KnownSmartFolderSets = new(StringComparer.OrdinalIgnoreCase)
    {
        "People", "Yearbook", "Trips", "Traditioner", "BedsteBillede", "UkendtePersoner",
        "Home", "Outside", "Hjemme", "Ude", "Kameraer", "Steder", "thumbnails",
        "RoterManuelt", "RotationUkendt",   // rotation review sets - working folders, not albums
    };

    // Friendly Danish labels for the root-level folders that do stay visible -
    // the folder name on disk never changes (other pipeline code depends on the
    // exact name), this only changes what is displayed to the viewer.
    public static readonly Dictionary<string, string> RootFolderDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Images"] = "Billeder",
        ["Videos"] = "Videoer",
        ["Audio"] = "Lyd",
        ["Documents"] = "Dokumenter",
    };

    // Billeder/Videoer are the primary content and should lead - everything else
    // sorts after, alphabetically among itself. QuickSort now sorts new libraries
    // straight into Danish-named category folders (see feedback_danish_folder_
    // defaults) rather than the English names this dictionary originally
    // assumed, so both variants are listed - an on-disk folder is one or the
    // other depending on when the tenant's library was first sorted, never both.
    public static readonly Dictionary<string, int> RootFolderSortPriority = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Images"] = 0, ["Billeder"] = 0,
        ["Videos"] = 1, ["Videoer"] = 1,
        ["Screenshots"] = 2, ["Skærmbilleder"] = 2,
    };

    // Root folders render as separate visual rows, not one flowing grid. Photo-
    // like content (Billeder/Videoer/Skærmbilleder) leads in the top row;
    // non-photo content (Dokumenter, LivePhotoVideoer, Musik) trails in the row
    // below it instead of mixing in - same Danish/English duplication reasoning
    // as RootFolderSortPriority above.
    public static readonly Dictionary<string, int> RootFolderRow = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Images"] = 0, ["Billeder"] = 0,
        ["Videos"] = 0, ["Videoer"] = 0,
        ["Screenshots"] = 0, ["Skærmbilleder"] = 0,
        ["Documents"] = 1, ["Dokumenter"] = 1,
        ["Musik"] = 1,
        ["LivePhotoVideoer"] = 1,
    };

    // Fallback icon shown when a root category card has no meaningful
    // content-based cover - either because none exists yet (Videos before any
    // thumbnail is generated) or because a real cover would never be
    // meaningful (Documents has no photo-like preview; Musik's own folder is
    // full of Windows Media Player's cached album art for whichever track
    // happens to be enumerated first, not a representative "cover" - see
    // HasNoCover). Images isn't listed - it always has real photo covers, a
    // generic icon would be a downgrade.
    public static readonly Dictionary<string, string> RootFolderIcon = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Videos"] = "🎬",
        ["Documents"] = "📄",
        ["Musik"] = "🎵",
    };

    // Documents/Musik never get a content-based cover at the root: a document has
    // no photo-like preview, and Musik's own folder is full of Windows Media
    // Player's cached album art for whichever track happens to enumerate first -
    // not a meaningful "cover" of the customer's own content.
    public static bool HasNoCover(string rootFolderName) =>
        rootFolderName.Equals("Documents", StringComparison.OrdinalIgnoreCase) ||
        rootFolderName.Equals("Musik", StringComparison.OrdinalIgnoreCase);

    // @eaDir/#recycle/#snapshot are Synology-generated system folders that can
    // appear at any depth (@eaDir in particular gets created wherever File
    // Station has ever touched a folder) - not something a customer should
    // ever see as a browsable folder.
    public static bool IsSystemFolder(string name) => name.StartsWith('.') || name.StartsWith('@') || name.StartsWith('#');

    // "thumbnails" is GalleryThumbnailService's own generated cache, sitting
    // inside every content folder at every depth.
    public static bool IsThumbnails(string name) => name.Equals("thumbnails", StringComparison.OrdinalIgnoreCase);

    // Older QuickSort output names month folders "NN-EnglishMonth" (e.g. "05-May")
    // regardless of gallery language - translate just the month word, at any
    // depth (not only the library root, unlike RootFolderDisplayNames), so
    // headers stay Danish once you've navigated into Billeder/2024/etc, not just
    // on the root folder cards. Newer QuickSort output (see feedback_danish_
    // folder_defaults) names them "N MonthName" already in Danish, no padding,
    // space instead of a hyphen - MonthFolderPattern/MonthLabelFor below handle
    // both shapes; this table only ever translates the older English form.
    private static readonly Dictionary<string, string> DanishMonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["January"] = "Januar", ["February"] = "Februar", ["March"] = "Marts",
        ["April"] = "April", ["May"] = "Maj", ["June"] = "Juni",
        ["July"] = "Juli", ["August"] = "August", ["September"] = "September",
        ["October"] = "Oktober", ["November"] = "November", ["December"] = "December",
    };
    private static readonly HashSet<string> DanishMonthNameValues = new(DanishMonthNames.Values, StringComparer.OrdinalIgnoreCase);

    // Optional " - 1. halvdel"/" - 2. halvdel" suffix (see LibraryExportService's
    // MonthHalfSplitThreshold) is captured separately so callers can either keep
    // it (DanishFolderName, for display) or strip it (MonthLabelFor, for grouping
    // both halves of a month back under one inline label).
    // Older QuickSort output: "NN-EnglishMonth" (zero-padded, hyphen, English -
    // needs DanishMonthNames translation below). Newer QuickSort output: "N
    // MonthName" (unpadded, space, already Danish - see feedback_danish_folder_
    // defaults). Both shapes must match or a month folder silently falls out of
    // chronological grouping entirely and back to a plain alphabetical folder
    // list, where "10 November"/"11 December" sort right after "1 Januar"
    // instead of at the end (found 2026-08-28 on a real tenant's 2013 folder).
    private static readonly Regex MonthFolderPattern = new(@"^(\d{1,2})[-\s]([A-Za-zÆØÅæøå]+(?:-[A-Za-zÆØÅæøå]+)?)( - [12]\. halvdel)?$");

    // LibraryExportService's current date-range grouping (recursive best-gap
    // bisection - see project_package1_month_split memory): either "dd-dd
    // MonthNameDanish" for a same-month range, or "Abbr-Abbr" for a cross-month
    // range. Same shape as LibraryVerifyService's GroupLabelPattern. Already Danish
    // and already a display-ready label as-is, unlike MonthFolderPattern above
    // (which names things in English and needs DanishMonthNames translation).
    private static readonly Regex DateRangeFolderPattern = new(@"^(\d{2}-\d{2} \p{L}+|[A-ZÆØÅ][a-zæøå]{2}-[A-ZÆØÅ][a-zæøå]{2})$");

    public static string DanishFolderName(string rawName)
    {
        var monthMatch = MonthFolderPattern.Match(rawName);
        if (!monthMatch.Success) return rawName;
        var monthWord = monthMatch.Groups[2].Value;
        // Already Danish (newer QuickSort output) - the raw name is already
        // display-ready as-is, reformatting it would just introduce a hyphen
        // where the real folder has a space.
        if (DanishMonthNameValues.Contains(monthWord)) return rawName;
        // Older English form - needs translating, and this shape always used a
        // zero-padded "NN-" prefix so reproducing that here is still correct.
        if (DanishMonthNames.TryGetValue(monthWord, out var danishMonth))
            return $"{monthMatch.Groups[1].Value}-{danishMonth}{monthMatch.Groups[3].Value}";
        return rawName;
    }

    // Just the month part, halvdel suffix stripped - both halves of a split
    // month collapse back into one inline label in the flattened view. Also
    // recognizes the newer date-range folder names, returned as-is since
    // they're already Danish and already a single (non-halvdel-split) group.
    public static string? MonthLabelFor(string rawName)
    {
        var m = MonthFolderPattern.Match(rawName);
        if (m.Success)
        {
            var monthWord = m.Groups[2].Value;
            var danish = DanishMonthNameValues.Contains(monthWord)
                ? monthWord
                : DanishMonthNames.TryGetValue(monthWord, out var dm) ? dm : monthWord;
            return $"{m.Groups[1].Value}-{danish}";
        }
        return DateRangeFolderPattern.IsMatch(rawName) ? rawName : null;
    }

    // Danish month abbreviation (3 letters, as used in the "Abbr-Abbr" date-range
    // folder form) -> calendar month number, for sorting that form chronologically.
    private static readonly Dictionary<string, int> DanishMonthAbbrToNumber = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jan"] = 1, ["Feb"] = 2, ["Mar"] = 3, ["Apr"] = 4, ["Maj"] = 5, ["Jun"] = 6,
        ["Jul"] = 7, ["Aug"] = 8, ["Sep"] = 9, ["Okt"] = 10, ["Nov"] = 11, ["Dec"] = 12,
    };

    // Leading calendar-month number for a month/date-range folder, purely for
    // chronological ordering - never shown to the user, so an imperfect guess
    // for an unrecognized shape (falls back to 0) just means "sorts first" rather
    // than crashing or breaking the grouping this exists to fix.
    public static int MonthSortKeyFor(string rawName)
    {
        var m = MonthFolderPattern.Match(rawName);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) return n;

        var digitMatch = Regex.Match(rawName, @"\d{1,2}");
        if (digitMatch.Success && int.TryParse(digitMatch.Value, out var d)) return d;

        var abbrMatch = Regex.Match(rawName, @"[A-ZÆØÅ][a-zæøå]{2}");
        if (abbrMatch.Success && DanishMonthAbbrToNumber.TryGetValue(abbrMatch.Value, out var mn)) return mn;

        return 0;
    }
}
