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

    public IEnumerable<string> EnumerateFiles(
        string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(
            rootPath,
            "*.*",
            SearchOption.AllDirectories);
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