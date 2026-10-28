using System.Collections.Concurrent;
using ITMartinGallery.Domain;
using static ITMartinGallery.Domain.LibraryPaths;
using static ITMartinGallery.Domain.MediaTypes;

namespace ITMartinGallery.Application;

public sealed record LibrarySummaryCounts(int TotalCount, int PhotoCount, int VideoCount, int AudioCount, int FolderCount, double TotalGb);
public sealed record CollectionCard(string Name, string Type, int FileCount, string? Cover);
public sealed record CollectionFile(string Name, string RelPath, string WebPath, string? Thumb, bool IsVideo, bool IsAudio, bool IsDoc, string? LiveVideo, string? Caption);
public sealed record PlaylistFile(string Name, string RelPath, string WebPath, bool IsVideo, bool IsAudio, bool IsDoc, string? LiveVideo);
public sealed record DayFile(string Name, string RelPath, string WebPath, string? Thumb, bool IsVideo);
public sealed record DayYear(int Year, List<DayFile> Files);
public sealed record SongSheet(string Title, string SourceFile, string Key, int? Tempo, string Chart, string Lyrics, string Notes, string Strum, string Fingerpick, bool Exact);

// The "Din samling er klar!" counts.
public sealed class LibrarySummary(ILibraryDisk disk)
{
    public LibrarySummaryCounts? Count(Gallery g)
    {
        if (!disk.DirectoryExists(g.Path)) return null;

        var hiddenFolders = g.HiddenRootFolders;
        var sep = Path.DirectorySeparatorChar;

        // Same root-level exclusions as folderCount below (hiddenFolders)
        // plus "thumbnails" wherever it appears - _Galleri/SmartFolders hold generated
        // thumbnails or symlinks back to files already counted, LivePhotos holds the
        // motion-clip companion to a still already counted in Images, and
        // DeleteCandidates holds flagged duplicates that aren't part of the real
        // collection. Leaving any of these in inflates the "Din samling er klar" stats.
        var mediaFiles = disk.AllFiles(g.Path, "*.*")
            .Where(f => !f.Contains($"{sep}thumbnails{sep}", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{sep}@eaDir{sep}", StringComparison.OrdinalIgnoreCase))
            .Where(f => hiddenFolders.All(hidden => !f.Contains($"{sep}{hidden}{sep}", StringComparison.OrdinalIgnoreCase)))
            .Where(f => !g.CoreCategoriesOnly || FolderNames.CoreCategoryNames.Contains(Rel(f, g.Path).Split('/')[0]))
            .Where(IsMedia)
            // Same reasoning as the folder browse view: a Musik folder's
            // cached album art and official music-video clips aren't real family
            // photos/videos and shouldn't inflate those counts.
            .Where(f => !Rel(f, g.Path).Split('/')[0].Equals("Musik", StringComparison.OrdinalIgnoreCase) || IsAud(Ext(f)));

        int photoCount = 0, videoCount = 0, audioCount = 0, totalCount = 0;
        long totalBytes = 0;
        foreach (var f in mediaFiles)
        {
            var ext = Ext(f);
            if (IsImg(ext)) photoCount++;
            else if (IsVid(ext)) videoCount++;
            else if (IsAud(ext)) audioCount++;
            totalCount++;
            totalBytes += disk.Length(f);
        }

        var folderCount = disk.Directories(g.Path)
            .Count(d => !Path.GetFileName(d).StartsWith('.') &&
                        !FolderNames.IsThumbnails(Path.GetFileName(d)) &&
                        !hiddenFolders.Contains(Path.GetFileName(d)));

        return new(totalCount, photoCount, videoCount, audioCount, folderCount, Math.Round(totalBytes / 1024.0 / 1024.0 / 1024.0, 1));
    }
}

// The "Samlinger" row (collections.json) and a folder as a playlist.
public sealed class CollectionsView(ILibraryDisk disk, ICollections collections, LibraryLinks links)
{
    // Keeps collections.json's own order (SmartFoldersService already orders each
    // kind the way it should read - trips smallest-first, yearbooks oldest-first)
    // rather than flattening everything into one biggest-first list regardless of kind.
    public List<CollectionCard> Cards(Gallery g) =>
        collections.Load(g.Path)
            .Where(c => c.FilePaths.Count > 0)
            .Select(c => new CollectionCard(c.Name, c.Type, c.FilePaths.Count,
                c.FilePaths.Select(f => links.TryThumbOrWeb(f, g.Path, g.Slug)).FirstOrDefault(w => w is not null)))
            .ToList();

    public List<CollectionFile>? Files(Gallery g, string name)
    {
        var col = collections.Load(g.Path).FirstOrDefault(c => c.Name == name);
        if (col is null) return null;

        // captions.json (written by the AI-billedtekster add-on) sits next to the
        // files themselves - one lookup per unique folder, not per file.
        var captionsByDir = new Dictionary<string, Dictionary<string, string>>();
        return col.FilePaths
            .Where(f => disk.FileExists(f) && IsSafe(f, g.Path))
            .Select(f =>
            {
                var ext = Ext(f);
                var dir = Path.GetDirectoryName(f)!;
                if (!captionsByDir.TryGetValue(dir, out var captions))
                    captionsByDir[dir] = captions = collections.Captions(dir);
                captions.TryGetValue(Path.GetFileName(f), out var caption);
                return new CollectionFile(Path.GetFileName(f), Rel(f, g.Path), links.Web(f, g.Path, g.Slug), links.TileThumb(f, g.Path, g.Slug),
                    IsVid(ext), IsAud(ext), IsDoc(ext), links.FindLivePhotoVideo(f, g.Path, g.Slug), caption);
            })
            .ToList();
    }

    public Answer<List<PlaylistFile>> Playlist(Gallery g, string folder)
    {
        var r = g.Path;
        var full = Path.GetFullPath(Path.Combine(r, folder));
        if (!disk.DirectoryExists(full)) return Answer<List<PlaylistFile>>.NotFound();
        if (!IsSafe(full, r)) return Answer<List<PlaylistFile>>.BadRequest("path outside library");

        return Answer<List<PlaylistFile>>.Ok(disk.Files(full)
            .Where(IsMedia)
            .OrderByDescending(disk.LastWriteUtc)
            .Select(f =>
            {
                var ext = Ext(f);
                return new PlaylistFile(Path.GetFileName(f), Rel(f, r), links.Web(f, r, g.Slug), IsVid(ext), IsAud(ext), IsDoc(ext),
                    links.FindLivePhotoVideo(f, r, g.Slug));
            })
            .ToList());
    }
}

// "På denne dag" and search. Both are live reads over manifest.json: "På denne dag" is a moving target (today's
// date changes daily) so it is never copied into its own SmartFolders subfolder, and the AI tags written by
// ImageTaggingService (Søgning & mærker add-on) live on the manifest's files - nothing to store on the Gallery side.
public sealed class ManifestViews(ILibraryDisk disk, IManifests manifests, LibraryLinks links)
{
    // manifest.json is QuickSort's full per-file record (~tens of thousands of
    // entries for a real library) - too big to re-parse on every "På denne dag"
    // homepage load. Long TTL because it only changes when Martin re-runs the
    // pipeline for that customer, which doesn't happen mid-session.
    private readonly ConcurrentDictionary<string, (DateTime Expires, IReadOnlyList<ManifestEntry>? Manifest)> _cache = new();

    private async Task<IReadOnlyList<ManifestEntry>?> Manifest(Gallery g, CancellationToken ct)
    {
        if (!_cache.TryGetValue(g.Slug, out var cached) || cached.Expires <= DateTime.UtcNow)
        {
            IReadOnlyList<ManifestEntry>? manifest = null;
            try { manifest = await manifests.LoadAsync(g.Path, ct); }
            catch { /* no manifest yet for this gallery - treat as empty */ }
            cached = (DateTime.UtcNow.AddHours(6), manifest);
            _cache[g.Slug] = cached;
        }
        return cached.Manifest;
    }

    private bool Showable(Gallery g, ManifestEntry f) =>
        f.ExportedPath is not null && disk.FileExists(f.ExportedPath) && IsSafe(f.ExportedPath, g.Path) &&
        (IsImg(Ext(f.ExportedPath)) || IsVid(Ext(f.ExportedPath)));

    private DayFile Tile(Gallery g, string path)
    {
        var ext = Ext(path);
        var wp = links.Web(path, g.Path, g.Slug);
        return new DayFile(Path.GetFileName(path), Rel(path, g.Path), wp,
            IsImg(ext) ? (links.Thumb(path, g.Path, g.Slug) ?? wp) : links.Thumb(path, g.Path, g.Slug), IsVid(ext));
    }

    public async Task<List<DayYear>> OnThisDay(Gallery g, DateTime today, CancellationToken ct)
    {
        var manifest = await Manifest(g, ct);
        if (manifest is null) return [];
        return manifest
            .Where(f => f.CreatedAt is { } d && d.Month == today.Month && d.Day == today.Day && d.Year < today.Year)
            .Where(f => Showable(g, f))
            .GroupBy(f => f.CreatedAt!.Value.Year)
            .OrderByDescending(gr => gr.Key)
            .Select(gr => new DayYear(gr.Key, gr.Select(f => Tile(g, f.ExportedPath!)).ToList()))
            .Where(y => y.Files.Count > 0)
            .ToList();
    }

    public async Task<List<DayFile>> Search(Gallery g, string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return [];
        var manifest = await Manifest(g, ct);
        if (manifest is null) return [];
        return manifest
            .Where(f => f.AiTags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Where(f => Showable(g, f))
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => Tile(g, f.ExportedPath!))
            .ToList();
    }
}

// Chords + lyrics for a song file, from the Musik Studio database.
public sealed class SongSheets(ISongSheetSource source)
{
    public SongSheet? For(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        var file = parts[^1];
        var folder = parts.Length > 1 ? parts[^2] : "";

        SongSheet? best = null; var bestScore = 0;
        foreach (var s in source.Songs())
        {
            var score = SongMatch.Score(s.SourceFile, s.Title, file, folder);
            if (score <= bestScore) continue;
            bestScore = score;
            best = new SongSheet(s.Title, s.SourceFile, s.Key, s.Tempo, s.Chart, s.Lyrics, s.Notes, s.Strum, s.Fingerpick, score == 3);
        }
        return best;
    }
}

// A file under /libraryfiles/<slug>/... and the music tag covers.
public sealed class LibraryFiles(ILibraryDisk disk, IEmbeddedCovers covers)
{
    public enum LinkKind { NotALink, Missing, Target }

    // PhysicalFileProvider/StaticFileMiddleware reports a symlink's own (lstat)
    // size rather than its target's - for SmartFolders' symlinked files that means
    // serving a handful of bytes (the length of the target path string) instead of
    // the real photo/video. Those are resolved here; real files go to static files.
    public (LinkKind Kind, string? Target) ResolveLink(Gallery g, string relative)
    {
        var physicalPath = Path.GetFullPath(Path.Combine(g.Path, relative));
        var target = disk.LinkTarget(physicalPath);
        if (target is null) return (LinkKind.NotALink, null);
        return disk.FileExists(target) ? (LinkKind.Target, target) : (LinkKind.Missing, null);
    }

    // Music tracks (Musik folders, mostly old MP3-rip collections) rarely have a
    // folder.jpg sitting next to them the way photo albums do - the actual cover
    // art is usually only embedded in the file's own ID3/MP4 tag.
    public Answer<(byte[] Data, string MimeType)> EmbeddedCover(Gallery g, string path)
    {
        var full = Path.GetFullPath(Path.Combine(g.Path, path));
        if (!IsSafe(full, g.Path)) return Answer<(byte[], string)>.BadRequest("path outside library");
        if (!disk.FileExists(full)) return Answer<(byte[], string)>.NotFound();
        return covers.Read(full) is { } cover ? Answer<(byte[], string)>.Ok(cover) : Answer<(byte[], string)>.NotFound();
    }
}
