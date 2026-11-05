using System.Collections.Concurrent;
using ITMartinGallery.Domain;
using static ITMartinGallery.Domain.FolderNames;
using static ITMartinGallery.Domain.LibraryPaths;
using static ITMartinGallery.Domain.MediaTypes;

namespace ITMartinGallery.Application;

// /api/browse: one folder of a library as folder cards + file tiles.
public sealed class FolderBrowser(ILibraryDisk disk, LibraryLinks links, PeopleIndex peopleIndex)
{
    // Browsing is read-heavy per folder navigation (a FolderCover() disk check
    // per subfolder, a FindLivePhotoVideo() pair of file checks per image) against
    // a library that essentially never changes mid-session - caching makes
    // forward/back navigation between already-visited folders instant instead of
    // re-walking the filesystem every time. Short TTL rather than no expiry so a
    // freshly-delivered library still shows up without a container restart.
    private readonly ConcurrentDictionary<string, (DateTime Expires, BrowsePage Payload)> _cache = new();

    public Answer<BrowsePage> Browse(Gallery g, string? path)
    {
        var cacheKey = $"{g.Slug}|{path}";
        if (_cache.TryGetValue(cacheKey, out var cachedBrowse) && cachedBrowse.Expires > DateTime.UtcNow)
            return Answer<BrowsePage>.Ok(cachedBrowse.Payload);

        var r = g.Path;
        var current = string.IsNullOrWhiteSpace(path) ? r : Path.GetFullPath(Path.Combine(r, path));

        // ".../Ukendt dato" that is not a real folder = the loose files of the folder above it.
        var looseView = false;
        if (!disk.DirectoryExists(current) && Path.GetFileName(current).Equals(LooseFolderName, StringComparison.OrdinalIgnoreCase) &&
            new DirectoryInfo(current).Parent?.FullName is { } looseParent && disk.DirectoryExists(looseParent) && !IsSameDir(looseParent, r))
        {
            current = looseParent;
            looseView = true;
        }

        if (!disk.DirectoryExists(current)) return Answer<BrowsePage>.NotFound();
        if (!IsSafe(current, r)) return Answer<BrowsePage>.BadRequest("path outside library");

        var atRoot = IsSameDir(current, r);
        var hiddenFolders = g.HiddenRootFolders;

        FolderEntry ToEntry(string d)
        {
            var rawName = Path.GetFileName(d);
            var noCoverCategory = atRoot && HasNoCover(rawName);
            return new(
                atRoot && RootFolderDisplayNames.TryGetValue(rawName, out var friendly) ? friendly : DanishFolderName(rawName),
                Rel(d, r),
                noCoverCategory ? null : links.FolderCover(d, r, g.Slug),
                atRoot && RootFolderRow.TryGetValue(rawName, out var row) ? row : 99,
                atRoot && RootFolderIcon.TryGetValue(rawName, out var icon) ? icon : null);
        }

        bool IsSystem(string d) => IsSystemFolder(Path.GetFileName(d));

        var priorityFolders = disk.Directories(current)
            .Where(d => !IsSystem(d))
            .Where(d => atRoot && RootFolderSortPriority.ContainsKey(Path.GetFileName(d)))
            .Where(links.HasAnyMediaFile)
            .OrderBy(d => RootFolderSortPriority[Path.GetFileName(d)])
            .Select(ToEntry)
            .ToList();

        var restFolders = disk.Directories(current)
            .Where(d => !IsSystem(d))
            // The root-level hidden-folder check below only applies at atRoot, so
            // without this a customer browsing into any year/month folder would see
            // a spurious "thumbnails" folder and could click into it to see a grid
            // of meaningless tiny crops.
            .Where(d => !IsThumbnails(Path.GetFileName(d)))
            .Where(d => !atRoot || (
                g.CoreCategoriesOnly
                    ? CoreCategoryNames.Contains(Path.GetFileName(d)) && !RootFolderSortPriority.ContainsKey(Path.GetFileName(d))
                    : !hiddenFolders.Contains(Path.GetFileName(d)) && !RootFolderSortPriority.ContainsKey(Path.GetFileName(d))))
            .Where(links.HasAnyMediaFile)
            // Plain alphabetical sorting a "1 Februar"/"10 November" shaped name
            // puts "10 November"/"11 December" right after "1 Februar" instead of
            // at the end - matches MonthSortKeyFor everywhere else. This only
            // fires when isFlattenableYear (below) didn't already turn these into
            // one continuous view - a year that also has a non-month sibling like
            // "Ukendt måned" fails that all-or-nothing check and falls back to
            // this folder-card list, so it still needs to sort correctly.
            // Calendar order (Januar..December), not newest-first - months read
            // as a sequence within a year, unlike years/dates which read newest-
            // first. Non-month folders (Ukendt måned) have no real position and
            // sort after all real months.
            .OrderByDescending(d => MonthLabelFor(Path.GetFileName(d)) is not null)
            .ThenBy(d => MonthSortKeyFor(Path.GetFileName(d)))
            .ThenBy(Path.GetFileName)
            .Select(ToEntry)
            .ToList();

        // SmartFolders' content (Home/Outside/People/Yearbook) is deliberately
        // NOT surfaced here as browsable folders - it's already shown once, as
        // the "Samlinger" row, via collections.json. Showing it twice, once
        // unlabeled in this plain folder grid and once as a labeled example row,
        // is confusing for a non-technical viewer - keep exactly one place it appears.
        var folders = priorityFolders.Concat(restFolders).ToList();
        if (looseView) folders = [];

        // View-only gallery (Galleries__N__ViewOnly): the root is exactly the
        // seven things a family wants to open and nothing else - no add-ons, no
        // locked placeholders, no collections rows, no duplicates/small-albums
        // clutter. The SmartFolders sets that the addons UI otherwise sells are
        // simply folders here, with Danish names. Asked for on 2026-09-11:
        // "Kun visning - fjern alt det man kan vælge."
        if (atRoot && g.ViewOnly)
        {
            folders = ViewOnlyRootFolders
                .Select(v => (Name: g.RootName(v), Full: Path.Combine(r, v.Rel), v.Rel, v.Icon, v.Row))
                .Where(v => g.ShowAtRoot(v.Rel) && disk.DirectoryExists(v.Full) && links.HasAnyMediaFile(v.Full))
                .Select(v => new FolderEntry(v.Name, v.Rel, links.FolderCover(v.Full, r, g.Slug), v.Row, v.Icon))
                .ToList();

            // "Billeder/2018,Billeder/2019": cards straight into a folder, in the order given (2026-11-05).
            foreach (var rel in g.DeepRootFolders.Where(x => !ViewOnlyRootFolders.Any(v => v.Rel.Equals(x, StringComparison.OrdinalIgnoreCase))))
            {
                var full = Path.Combine(r, rel.Replace('/', Path.DirectorySeparatorChar));
                if (disk.DirectoryExists(full) && links.HasAnyMediaFile(full))
                    folders.Add(new FolderEntry(Path.GetFileName(rel), NormalizeRel(rel), links.FolderCover(full, r, g.Slug), 0, "🖼️"));
            }

            var smartRoot = Path.Combine(r, "SmartFolders");
            if (disk.DirectoryExists(smartRoot) && string.IsNullOrWhiteSpace(g.RootFolders))
            {
                folders.AddRange(disk.Directories(smartRoot)
                    .Where(d => !IsSystem(d) && !KnownSmartFolderSets.Contains(Path.GetFileName(d)))
                    .Where(links.HasAnyMediaFile)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .Select(d => new FolderEntry(Path.GetFileName(d), NormalizeRel(Rel(d, r)), links.FolderCover(d, r, g.Slug), 2, "📖")));
            }
        }

        // Musik folders are full of Windows Media Player's cached album art
        // (AlbumArt_{GUID}_Large.jpg, Folder.jpg) sitting next to the actual
        // tracks - browsing in there should show the music, not a wall of cover
        // art thumbnails that aren't even a real photo of anything.
        var inMusikFolder = Rel(current, r).Split('/')[0].Equals("Musik", StringComparison.OrdinalIgnoreCase);

        // A Year folder whose only children are Month folders (LibraryExportService
        // only creates these once a year is "busy" - see MonthSplitThreshold) reads
        // as an extra click for no reason - flatten it into one continuous view with
        // inline month labels instead of forcing a folder-per-month click. Never
        // applies inside Musik (organized by Artist/Album, not Year/Month at all).
        // "- 1. halvdel"/"- 2. halvdel" (see MonthHalfSplitThreshold) collapse back
        // into one label - that split exists for the static HD export's page size,
        // not something the live viewer needs to expose as a separate group.
        var isFlattenableYear =
            !inMusikFolder && !atRoot && folders.Count > 0 &&
            folders.All(f => MonthLabelFor(Path.GetFileName(f.relPath)) is not null);

        IEnumerable<string> filePaths;
        Dictionary<string, string>? monthLabelByPath = null;
        // Chronological sort key per month folder (the leading month number, e.g.
        // "02-Februar" -> 2) - see the ordering fix below for why this exists.
        Dictionary<string, int>? monthSortKeyByPath = null;

        if (isFlattenableYear)
        {
            monthLabelByPath = new Dictionary<string, string>();
            monthSortKeyByPath = new Dictionary<string, int>();
            var monthDirs = disk.Directories(current).Where(d => !IsSystem(d) && !IsThumbnails(Path.GetFileName(d)));
            var gathered = new List<string>();
            foreach (var monthDir in monthDirs)
            {
                var folderName = Path.GetFileName(monthDir);
                var label = MonthLabelFor(folderName);
                if (label is null) continue;
                var sortKey = MonthSortKeyFor(folderName);
                foreach (var f in disk.Files(monthDir).Where(IsMedia))
                {
                    gathered.Add(f);
                    monthLabelByPath[f] = label;
                    monthSortKeyByPath[f] = sortKey;
                }
            }
            filePaths = gathered;
            folders = []; // flattened - no separate Month folder cards
        }
        else if (folders.Count > 0)
        {
            // A handful of stray files sitting directly in a folder that also has
            // real subfolders to navigate (e.g. loose photos straight in
            // "Billeder" alongside its year folders) renders as a confusing
            // second, unlabelled "Billeder & videoer" grid competing with the
            // real navigation - and QuickSort already has a real home for
            // deliberately-orphaned files ("Ikke i årsmapper"/Udaterede), so this
            // is never the only place they'd be findable. Suppress it here;
            // still shown normally one level deeper where there's nothing else
            // to navigate to and these ARE the real content.
            filePaths = [];

            // ...but not hidden altogether: they get a "Ukendt dato" card after the real folders, unless the library
            // already has its own folder for them.
            var loose = disk.Files(current).Where(IsMedia).ToList();
            if (loose.Count > 0 && !atRoot && !inMusikFolder &&
                !folders.Any(f => f.name.Contains("dato", StringComparison.OrdinalIgnoreCase) ||
                                  f.name.Contains("årsmapper", StringComparison.OrdinalIgnoreCase) ||
                                  f.name.Contains("Udaterede", StringComparison.OrdinalIgnoreCase)))
            {
                var first = loose.FirstOrDefault(f => IsImg(Ext(f))) ?? loose[0];
                folders.Add(new FolderEntry(LooseFolderName, NormalizeRel(Rel(current, r)) + "/" + LooseFolderName,
                    IsImg(Ext(first)) ? links.Thumb(first, r, g.Slug) ?? links.Web(first, r, g.Slug) : links.Thumb(first, r, g.Slug)));
            }
        }
        else
        {
            filePaths = disk.Files(current).Where(IsMedia);
        }

        // Filesystem LastWriteTimeUtc alone is NOT a safe sort key across a
        // flattened multi-month view - repeated copy/re-import passes on a real
        // tenant library leave this timestamp reflecting when a file was last
        // copied in, not when it was taken, so files from different months end
        // up interleaved. That interleaving is what made the month-divider
        // header (see wwwroot/index.html) flip back and forth and appear to
        // repeat instead of showing once per month. Grouping by the month's own
        // chronological key first keeps every month's files contiguous; within
        // a month, LastWriteTimeUtc is still a reasonable enough tiebreaker.
        // Theme "bedstemor": names of the family members in each picture (from the person folders).
        var withPeople = PeopleNames.WithPeople(g);
        var peopleMap = withPeople ? peopleIndex.Get(r).Map : null;
        var files = filePaths
            .Where(f => !inMusikFolder || IsAud(Ext(f)))
            // Calendar order within the year (Januar..December), matching the
            // folder-card fallback's ordering - months read as a sequence,
            // not newest-first.
            .OrderBy(f => monthSortKeyByPath?.GetValueOrDefault(f) ?? 0)
            .ThenByDescending(disk.LastWriteUtc)
            .Select(f =>
            {
                var ext = Ext(f);
                return new FileTile(
                    Path.GetFileName(f),
                    Rel(f, r),
                    links.Web(f, r, g.Slug),
                    links.TileThumb(f, r, g.Slug),
                    IsVid(ext),
                    IsAud(ext),
                    IsDoc(ext),
                    links.FindLivePhotoVideo(f, r, g.Slug),
                    monthLabelByPath?.GetValueOrDefault(f),
                    withPeople && IsImg(ext) ? peopleIndex.For(peopleMap, f) : null);
            })
            .ToList();

        var parentFull = atRoot ? null : new DirectoryInfo(current).Parent?.FullName;
        var parentRel = parentFull is null ? null : NormalizeRel(Rel(parentFull, r));

        // View-only: SmartFolders itself is never a page. Going back from
        // Personer/Rejser/Årbog lands on the front page, not on a raw folder
        // listing the viewer was never shown; and the header says "Personer",
        // not "People".
        string? title = null;
        if (g.ViewOnly && !atRoot)
        {
            var relNow = NormalizeRel(Rel(current, r));
            if (parentRel is not null && parentRel.Equals("SmartFolders", StringComparison.OrdinalIgnoreCase))
                parentRel = "";
            title = ViewOnlyRootFolders.Where(v => v.Rel.Equals(relNow, StringComparison.OrdinalIgnoreCase)).Select(g.RootName).FirstOrDefault()
                // A curated album straight under SmartFolders is titled by its folder name.
                ?? (parentRel == "" ? Path.GetFileName(current) : null);
        }

        if (looseView)
        {
            parentRel = NormalizeRel(Rel(current, r));
            title = LooseFolderName;
        }

        var people = withPeople && atRoot
            ? peopleIndex.Get(r).Names.Select(n => new PersonLink(PeopleNames.DisplayName(n), PeopleNames.PeopleRel + "/" + n)).ToArray()
            : null;
        var page = new BrowsePage(atRoot, parentRel, folders, files, g.HideAddons || g.ViewOnly, g.ViewOnly, title,
            g.Theme, g.Headline, g.Tagline, g.UploadUrl, people);
        _cache[cacheKey] = (DateTime.UtcNow.AddMinutes(10), page);
        return Answer<BrowsePage>.Ok(page);
    }
}
