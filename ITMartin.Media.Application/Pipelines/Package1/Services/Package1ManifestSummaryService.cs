using System.Text.Json;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.Package1.Services;

public sealed class Package1ManifestSummaryService
{
    public async Task<Package1ScanResult> LoadAsync(
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
                Package1Manifest>(json);

        if (manifest is null)
        {
            throw new InvalidOperationException(
                "Failed to deserialize manifest");
        }

        return new Package1ScanResult
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