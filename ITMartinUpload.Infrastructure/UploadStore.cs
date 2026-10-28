using ITMartinUpload.Application;
using ITMartinUpload.Domain;
using Microsoft.Extensions.Options;
using static ITMartinUpload.Domain.UploadNames;

namespace ITMartinUpload.Infrastructure;

/// <summary>
/// The customer folders on disk (the NAS volume): saving under a free name, listing, and moving a file to the trash
/// folder. The naming rules are in <see cref="UploadNames"/>.
/// </summary>
public sealed class UploadStore : IUploadFiles
{
    private readonly StorageOptions _options;

    public UploadStore(IOptions<StorageOptions> options) : this(options.Value) { }

    public UploadStore(StorageOptions options)
    {
        _options = options;
        Directory.CreateDirectory(_options.Root);
    }

    public string FolderFor(string slug) =>
        Path.Combine(_options.Root, slug.ToLowerInvariant());

    public async Task<string> SaveAsync(string slug, string safeName, Stream content, CancellationToken ct = default, string? subfolder = null)
    {
        var folder = subfolder is null ? FolderFor(slug) : Path.Combine(FolderFor(slug), subfolder);
        Directory.CreateDirectory(folder);
        var dest = UniquePath(folder, safeName, File.Exists);

        // Written under a temporary name and renamed when complete, so a list taken
        // mid-upload (the event feed polls every few seconds) never shows a half file.
        var part = dest + PartSuffix;
        try
        {
            await using (var file = File.Create(part))
                await content.CopyToAsync(file, ct);
            File.Move(part, dest);
        }
        catch
        {
            try { File.Delete(part); } catch (IOException) { }
            throw;
        }
        return dest;
    }

    /// <summary>
    /// True when this customer already sent a file with the same name and the same size.
    /// Good enough to stop the common "did I send these already?" re-upload without
    /// hashing every byte of a 4 GB video on the customer's phone.
    /// </summary>
    public bool AlreadyHave(string slug, string fileName, long size)
    {
        var safeName = SafeFileName(fileName);
        if (safeName is null) return false;
        return List(slug).Any(f =>
            f.Size == size &&
            string.Equals(f.Name, safeName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when that exact file (same folder, name and size) is already here - a Rip CD
    /// retry after a dropped connection must not leave "01 Changes_1.flac" next to the first one.</summary>
    public bool AlreadyHaveIn(string slug, string? subfolder, string safeName, long size)
    {
        var path = subfolder is null ? Path.Combine(FolderFor(slug), safeName) : Path.Combine(FolderFor(slug), subfolder, safeName);
        return File.Exists(path) && new FileInfo(path).Length == size;
    }

    /// <summary>
    /// Resolves a file the customer refers to by name, inside their own folder only.
    /// Returns null when the name is not one of theirs - never a path outside the folder.
    /// </summary>
    public string? ResolveOwnFile(string slug, string fileName)
    {
        var safeName = SafeFileName(fileName);
        if (safeName is null || !IsValidSlug(slug)) return null;

        var folder = FolderFor(slug);
        var full = Path.GetFullPath(Path.Combine(folder, safeName));
        if (!full.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return null;

        return File.Exists(full) ? full : null;
    }

    /// <summary>
    /// Takes a file out of the customer's folder when they say it should not have been
    /// sent. It is moved into <see cref="TrashFolderName"/>, not deleted: if they regret it,
    /// or the wrong one goes, the file is still on the NAS.
    /// </summary>
    public bool Remove(string slug, string fileName)
    {
        var source = ResolveOwnFile(slug, fileName);
        if (source is null) return false;

        var trash = Path.Combine(FolderFor(slug), TrashFolderName);
        Directory.CreateDirectory(trash);
        var dest = UniquePath(trash, Path.GetFileName(source), File.Exists);
        File.Move(source, dest);
        return true;
    }

    public IReadOnlyList<StoredFile> List(string slug)
    {
        try
        {
            var folder = FolderFor(slug);
            if (!Directory.Exists(folder)) return [];
            return Directory.GetFiles(folder)
                .Where(p => !p.EndsWith(PartSuffix, StringComparison.OrdinalIgnoreCase))
                .Select(p => new FileInfo(p))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .Select(fi => new StoredFile(fi.Name, fi.Length, fi.LastWriteTimeUtc))
                .ToList();
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    /// <summary>What a music customer has sent, album by album (the Artist/Album folders), newest first.</summary>
    public IReadOnlyList<StoredAlbum> Albums(string slug)
    {
        try
        {
            var folder = FolderFor(slug);
            if (!Directory.Exists(folder)) return [];
            return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Where(IsAudio)
                .Select(p => new FileInfo(p))
                .Select(fi => (fi, dir: Path.GetRelativePath(folder, fi.DirectoryName!)))
                .Where(x => !x.dir.Split(Path.DirectorySeparatorChar)[0].Equals(TrashFolderName, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.dir)
                .Select(g => new StoredAlbum(
                    g.Key == "." ? "Løse numre" : string.Join(" – ", g.Key.Split(Path.DirectorySeparatorChar)),
                    g.Count(), g.Sum(x => x.fi.Length), g.Max(x => x.fi.LastWriteTimeUtc)))
                .OrderByDescending(a => a.ModifiedUtc)
                .ToList();
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }
}
