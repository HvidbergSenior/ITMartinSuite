using System.Text.Json;
using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Services;

public sealed class QuickSortManifestSummaryService
{
    public async Task<QuickSortScanResult> LoadAsync(
        string exportRoot,
        CancellationToken cancellationToken)
    {
        var manifestPath =
            Path.Combine(
                exportRoot,
                "manifest.json");

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                "manifest.json not found",
                manifestPath);
        }

        var json =
            await File.ReadAllTextAsync(
                manifestPath,
                cancellationToken);

        var manifest =
            JsonSerializer.Deserialize<
                QuickSortManifest>(json);

        if (manifest is null)
        {
            throw new InvalidOperationException(
                "Failed to deserialize manifest");
        }

        return new QuickSortScanResult
        {
            TotalFiles =
                manifest.MediaFiles.Count,

            KeepCount =
                manifest.MediaFiles.Count(x =>
                    x.ExportedPath is not null),

            DeleteCount =
                manifest.MediaFiles.Count(x =>
                    x.ExportedPath is null),

            DuplicateGroups =
                manifest.MediaFiles
                    .GroupBy(x => x.Hash)
                    .Count(x => x.Count() > 1),

            TotalBytes =
                manifest.MediaFiles.Sum(x =>
                    x.SizeBytes),

            BytesToDelete =
                manifest.MediaFiles
                    .Where(x =>
                        x.ExportedPath is null)
                    .Sum(x =>
                        x.SizeBytes),

            Files =
                manifest.MediaFiles.ToList()
        };
    }
}