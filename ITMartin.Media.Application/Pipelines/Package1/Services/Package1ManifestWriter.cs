using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Pipelines.Package1.Services;

public sealed class Package1ManifestWriter
{
    // manifest.json gets rewritten very frequently across a run (every
    // checkpoint, every add-on step) and intermittently throws
    // UnauthorizedAccessException on Windows for a few hundred ms right after
    // a write, even with no other app holding it open - consistent with
    // real-time antivirus/indexer scanning briefly locking a just-modified
    // file, not a real sharing conflict (confirmed by hand: a plain
    // PowerShell File.WriteAllText on the same path fails identically at the
    // same moment). Root cause was never pinned down further in an earlier
    // investigation - this is a pragmatic retry for a known-flaky Windows
    // I/O pattern rather than a fix for a specific bug.
    private const int MaxAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(400);

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

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await File.WriteAllTextAsync(
                    manifestPath,
                    json,
                    cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && (ex is UnauthorizedAccessException or IOException))
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }
}