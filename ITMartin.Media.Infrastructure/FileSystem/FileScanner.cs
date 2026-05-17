using ITMartin.Media.Domain.Interfaces;

namespace ITMartin.Media.Infrastructure.FileSystem;

using ITMartin.Media.Interfaces;

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
}