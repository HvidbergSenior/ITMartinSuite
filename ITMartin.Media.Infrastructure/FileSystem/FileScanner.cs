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

    private static readonly HashSet<string> SkippedFolders =
    [
        "@eadir", "@eaDir", "#recycle", "#snapshot",
        ".@__thumb", "@recently-snapshot", ".synophoto",
        ".package1", ".package2", "thumbnails", "SmartFolders"
    ];

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