using System.Collections.Concurrent;
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Enums;
using ITMartin.Media.Domain.Interfaces;
using ITMartin.Media.Interfaces;
using ITMartin.Media.Enums;
namespace ITMartin.Media.Application.Processors;

public class ParallelScanProcessor
{
    private readonly IFileScanner
        _fileScanner;

    public ParallelScanProcessor(
        IFileScanner fileScanner)
    {
        _fileScanner =
            fileScanner;
    }

    public async Task<List<MediaFile>>
        ProcessAsync(
            IEnumerable<string> paths,
            Action<int, int, string>?
                progress = null,
            int maxDegreeOfParallelism = 4)
    {
        var allPaths =
            paths.ToList();

        int total =
            allPaths.Count;

        int done = 0;

        var bag =
            new ConcurrentBag<MediaFile>();

        await Parallel.ForEachAsync(
            allPaths,
            new ParallelOptions
            {
                MaxDegreeOfParallelism =
                    maxDegreeOfParallelism
            },
            async (filePath, ct) =>
            {
                var file =
                    _fileScanner
                        .ProcessFile(
                            filePath,
                            ScanMode.FastCleanup);
                
                if (file != null)
                {
                    bag.Add(file);
                }

                var current =
                    Interlocked
                        .Increment(ref done);

                progress?.Invoke(
                    current,
                    total,
                    Path.GetFileName(
                        filePath));

                await Task.CompletedTask;
            });

        return bag.ToList();
    }
}