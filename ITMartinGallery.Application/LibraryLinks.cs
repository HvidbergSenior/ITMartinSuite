using ITMartinGallery.Domain;
using static ITMartinGallery.Domain.LibraryPaths;
using static ITMartinGallery.Domain.MediaTypes;

namespace ITMartinGallery.Application;

// The addresses the pages use for a library file: the file itself, its thumbnail, a folder cover, a Live Photo clip.
public sealed class LibraryLinks(ILibraryDisk disk)
{
    // ?v=<last write>: a replaced file (a new take of a song) gets a new address, so no cache serves the old one.
    public string Web(string abs, string r, string slug) =>
        $"/libraryfiles/{slug}/" + Rel(abs, r) + (disk.FileExists(abs) ? "?v=" + disk.LastWriteUtc(abs).Ticks : "");

    public static string AudioCover(string f, string r, string slug) => $"/api/embedded-cover?gallery={slug}&path={Uri.EscapeDataString(Rel(f, r))}";

    // Samlinger entries (Person/Trip/Yearbook) are SmartFolders' own real copies
    // (never symlinks - see ISmartFoldersService docs), so their thumbnails live
    // in a "thumbnails" folder right next to them, same as everywhere else.
    public string? Thumb(string f, string r, string slug)
    {
        var t = Path.Combine(Path.GetDirectoryName(f)!, "thumbnails", Path.GetFileNameWithoutExtension(f) + ".jpg");
        return disk.FileExists(t) ? Web(t, r, slug) : null;
    }

    // The static export's thumbnails: _Galleri/thumbs/<same relative folder>/<name>.jpg.
    // Person folders get theirs there rather than in a thumbnails/ subfolder.
    // Versioned by mtime (Web adds ?v=): Cloudflare caches thumbnails for hours, so a
    // regenerated (e.g. un-rotated) thumbnail would otherwise keep showing the old one.
    public string? GalleriThumb(string f, string r, string slug)
    {
        var rel = Rel(f, r);
        var t = Path.Combine(r, "_Galleri", "thumbs", Path.GetDirectoryName(rel) ?? "", Path.GetFileNameWithoutExtension(f) + ".jpg");
        return disk.FileExists(t) ? Web(t, r, slug) : null;
    }

    // The tile picture for any media file: images fall back to themselves, videos only
    // ever get a real thumbnail (a browser can't show an mp4 as an <img>), music its tag cover.
    public string? TileThumb(string f, string r, string slug)
    {
        var ext = Ext(f);
        return IsImg(ext) ? (Thumb(f, r, slug) ?? Web(f, r, slug)) : (IsVid(ext) ? Thumb(f, r, slug) : (IsAud(ext) ? AudioCover(f, r, slug) : null));
    }

    public string? TryThumbOrWeb(string f, string r, string slug) =>
        disk.FileExists(f) && IsSafe(f, r) ? (Thumb(f, r, slug) ?? Web(f, r, slug)) : null;

    // The still and its Live Photo motion clip are exported into separate
    // top-level folders (Images/ vs LivePhotos/) by QuickSort, connected only by
    // matching Year/Month/filename - there's no explicit link stored anywhere.
    // This reconstructs that link at read time, same idea as QuickSort's own
    // LivePhotoDetectionWorkflowStep pairing logic, just applied on already-organized output.
    public string? FindLivePhotoVideo(string imagePath, string r, string slug)
    {
        if (!IsImg(Ext(imagePath))) return null;

        var rel = Rel(imagePath, r);
        if (!rel.StartsWith("Images/", StringComparison.OrdinalIgnoreCase)) return null;

        var withoutExt = Path.ChangeExtension(rel, null) ?? rel;
        var livePhotoRel = "LivePhotos" + withoutExt["Images".Length..];

        foreach (var vidExt in new[] { ".mp4", ".mov" })
        {
            var candidate = Path.Combine(r, livePhotoRel + vidExt);
            if (disk.FileExists(candidate))
                return Web(candidate, r, slug);
        }

        return null;
    }

    // Year-level folders (e.g. "Images/2025") never have any files directly
    // inside them - the actual photos/thumbnails live one level deeper, under
    // each month subfolder. Only checking the immediate folder meant every
    // year/top-level card showed a generic placeholder instead of a real cover -
    // searches down a few levels (month folders, not the whole library) for the
    // first usable cover instead of just the immediate directory.
    public string? FolderCover(string dir, string r, string slug, int depth = 3)
    {
        var td = Path.Combine(dir, "thumbnails");
        if (disk.DirectoryExists(td))
        {
            // Directory enumeration has no guaranteed order, and a stray
            // zero-byte file (e.g. a leftover/failed thumbnail generation) sitting
            // in this folder could win the pick by pure filesystem-enumeration
            // luck, rendering as a broken image client-side. Sort for determinism
            // and skip anything that isn't a real file.
            var t = disk.Files(td, "*.jpg")
                .Where(f => disk.Length(f) > 0)
                .OrderBy(Path.GetFileName)
                .FirstOrDefault();
            if (t is not null) return Web(t, r, slug);
        }

        // Images render fine as a raw <img src> fallback with no thumbnail yet;
        // a video file wouldn't (browsers can't show an mp4 as an image) - videos
        // only ever get a cover via their thumbnails/ entry, checked above.
        var direct = disk.Files(dir).FirstOrDefault(f => IsImg(Ext(f)));
        if (direct is not null) return Web(direct, r, slug);

        if (depth <= 0) return null;

        foreach (var subDir in disk.Directories(dir).OrderBy(Path.GetFileName))
        {
            var name = Path.GetFileName(subDir);
            if (name.StartsWith('.') || FolderNames.IsThumbnails(name)) continue;

            var cover = FolderCover(subDir, r, slug, depth - 1);
            if (cover is not null) return cover;
        }

        return null;
    }

    // A category folder (Lyd/Memes/Screenshots/...) with zero files anywhere
    // underneath it - e.g. an add-on that never actually produced anything for
    // this tenant - still showed up as a clickable card leading to an empty
    // "Ingen medietiler her" page. Short-circuits on the first file found, so
    // a populated folder costs one listing, not a full walk.
    public bool HasAnyMediaFile(string dir)
    {
        foreach (var f in disk.Files(dir))
            if (IsMedia(f)) return true;

        foreach (var sub in disk.Directories(dir))
        {
            var name = Path.GetFileName(sub);
            if (FolderNames.IsSystemFolder(name)) continue;
            if (FolderNames.IsThumbnails(name)) continue;
            if (HasAnyMediaFile(sub)) return true;
        }
        return false;
    }
}
