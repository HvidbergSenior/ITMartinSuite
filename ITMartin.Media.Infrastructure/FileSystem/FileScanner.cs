using System.IO.Compression;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Infrastructure.Media;

namespace ITMartin.Media.Infrastructure.FileSystem;

public sealed class FileScanner : IFileScanner
{
    public Task<IEnumerable<string>> ScanAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        IEnumerable<string> files = EnumerateFiles(rootPath);

        return Task.FromResult(files);
    }

    private static readonly HashSet<string> SkippedFolders = new(
        [
            "@eadir", "#recycle", "#snapshot",
            ".@__thumb", "@recently-snapshot", ".synophoto",
            ".package1", ".package2", "thumbnails", "SmartFolders", "_Galleri",
            // Windows/OS system folders — never real content, and $RECYCLE.BIN's
            // per-user subfolders are access-denied to a normal process, which
            // previously crashed the whole scan rather than just skipping it.
            "$RECYCLE.BIN", "System Volume Information"
        ],
        StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> EnumerateFiles(
        string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        return EnumerateFilesRecursive(rootPath);
    }

    private static IEnumerable<string> EnumerateFilesRecursive(
        string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            // Raw source folders sometimes contain photo/document exports as
            // zips (iCloud exports, phone backups, etc.). Extract in place so
            // the contents flow through normal discovery/classification; the
            // extracted sibling folder is picked up by the directory walk
            // below. On extraction failure, fall through and yield the zip
            // itself so it still lands in Unhandled rather than disappearing.
            if (IsZipArchive(file) && TryEnsureZipExtracted(file, out _))
            {
                continue;
            }

            yield return file;
        }

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subDir);
            if (SkippedFolders.Contains(name) ||
                name.StartsWith('@') ||
                name.StartsWith('#') ||
                name.StartsWith('.'))
            {
                continue;
            }

            foreach (var file in EnumerateFilesRecursive(subDir))
            {
                yield return file;
            }
        }
    }

    private static bool IsZipArchive(string path) =>
        string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);

    private static bool TryEnsureZipExtracted(string zipPath, out string extractedDir)
    {
        var baseDir = Path.GetDirectoryName(zipPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(zipPath);

        // A sibling folder with the exact same name (no suffix) already
        // existing means someone (Explorer, a prior manual extract) already
        // unpacked this zip — extracting again into a "_extracted" sibling
        // would duplicate every file inside it. Only fall back to our own
        // "_extracted" folder when no such sibling exists.
        var alreadyExtracted = Path.Combine(baseDir, baseName);
        if (Directory.Exists(alreadyExtracted))
        {
            extractedDir = alreadyExtracted;
            return true;
        }

        extractedDir = Path.Combine(baseDir, baseName + "_extracted");

        if (Directory.Exists(extractedDir))
        {
            return true;
        }

        try
        {
            ZipFile.ExtractToDirectory(zipPath, extractedDir);
            return true;
        }
        catch
        {
            if (Directory.Exists(extractedDir))
            {
                Directory.Delete(extractedDir, recursive: true);
            }

            return false;
        }
    }
    public MediaFile? ProcessFile(
        string path,
        ScanMode scanMode)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var fileInfo =
            new FileInfo(path);

        var mediaType =
            MediaTypeHelper.GetMediaType(path);

        return new MediaFile(
            path,
            fileInfo.CreationTimeUtc,
            mediaType,
            fileInfo.Length);
    }
}