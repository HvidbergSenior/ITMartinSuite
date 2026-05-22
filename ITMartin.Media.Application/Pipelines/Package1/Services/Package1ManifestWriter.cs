using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.Package1.Services;

public sealed class Package1ManifestWriter
{
    public async Task WriteAsync(
        string exportPath,
        Package1Manifest manifest,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(
            exportPath);

        var manifestPath =
            Path.Combine(
                exportPath,
                "manifest.json");

        var json =
            JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        await File.WriteAllTextAsync(
            manifestPath,
            json,
            cancellationToken);
    }
}