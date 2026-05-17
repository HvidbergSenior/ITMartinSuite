using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Enums;
using ITMartin.Media.Domain.Interfaces;

namespace ITMartin.Media.Application.Abstractions.Strategies.Scanning;

public sealed class FastScanStrategy
    : IScanStrategy
{
    private readonly IFileScanner
        _fileScanner;

    public FastScanStrategy(
        IFileScanner fileScanner)
    {
        _fileScanner =
            fileScanner;
    }

    public string Name => "Fast";

    public Task<IReadOnlyCollection<MediaFile>>
        ExecuteAsync(
            string path,
            CancellationToken cancellationToken)
    {
        IReadOnlyCollection<MediaFile>
            files =
                _fileScanner
                    .EnumerateFiles(path)
                    .Select(x =>
                        _fileScanner.ProcessFile(
                            x,
                            ScanMode.FastCleanup))
                    .Where(x => x != null)
                    .Cast<MediaFile>()
                    .ToList();

        return Task.FromResult(files);
    }
}