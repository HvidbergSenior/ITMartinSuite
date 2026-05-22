using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.Package2.Services;

public sealed class Package1ManifestLoader
{
    public async Task<Package1Manifest> LoadAsync(
        string sourceLibraryPath,
        CancellationToken cancellationToken)
    {
        var manifestPath =
            Path.Combine(
                sourceLibraryPath,
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

        return manifest;
    }
}