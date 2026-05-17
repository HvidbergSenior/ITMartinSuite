using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Enums;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Enums;

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

        return new MediaFile(
            path,
            fileInfo.CreationTimeUtc,
            MediaType.Image,
            fileInfo.Length);
    }
}