using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Services;

public sealed class QuickSortManifestLoader
{
    public async Task<QuickSortManifest> LoadAsync(
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
                QuickSortManifest>(json);

        if (manifest is null)
        {
            throw new InvalidOperationException(
                "Failed to deserialize manifest");
        }

        // Rebase paths if the manifest was written with a different root
        // (e.g. Windows Z:\ paths when now running on Linux in Docker)
        if (!string.IsNullOrWhiteSpace(manifest.RootPath) &&
            !PathsAreEquivalent(manifest.RootPath, sourceLibraryPath))
        {
            RebasePaths(manifest, sourceLibraryPath);
        }

        return manifest;
    }

    private static bool PathsAreEquivalent(string a, string b)
    {
        return string.Equals(
            Normalize(a),
            Normalize(b),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimEnd('/');

    private static void RebasePaths(QuickSortManifest manifest, string newRoot)
    {
        var oldRoot = Normalize(manifest.RootPath).TrimEnd('/') + "/";
        var newRootNorm = newRoot.TrimEnd('/', '\\');

        foreach (var file in manifest.MediaFiles)
        {
            if (file.ExportedPath is not null)
                file.ExportedPath = Rebase(file.ExportedPath, oldRoot, newRootNorm);

            if (file.NormalizedPath is not null)
                file.NormalizedPath = Rebase(file.NormalizedPath, oldRoot, newRootNorm);

            if (file.ThumbnailPath is not null)
                file.ThumbnailPath = Rebase(file.ThumbnailPath, oldRoot, newRootNorm);
        }
    }

    private static string Rebase(string filePath, string oldRootSlash, string newRoot)
    {
        var normalized = Normalize(filePath);
        if (!normalized.StartsWith(oldRootSlash, StringComparison.OrdinalIgnoreCase))
            return filePath;

        var relative = normalized[oldRootSlash.Length..].Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(newRoot, relative);
    }
}