using System.IO.Compression;
using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

/// <summary>
/// Runs before FileDiscovery. Recursively finds .zip archives under the
/// source root and extracts each one into a sibling "&lt;name&gt;_extracted"
/// folder (the same naming FileSorter users have already been using by hand
/// for years - e.g. "iCloud-fotos (7)_extracted"), then archives the
/// original .zip to .zip-source/ so FileScanner never sees it.
///
/// Confirmed 2026-09-07: no zip-handling existed anywhere in QuickSort - a
/// .zip sitting in a raw source folder (a whole-drive backup dump routinely
/// contains several, per past iCloud/Dropbox export folders seen on real
/// customer libraries) was carried through FileDiscovery as one opaque
/// "Other" file, its contents never seen or sorted.
/// </summary>
public sealed class ZipExtractionWorkflowStep : QuickSortWorkflowStepBase
{
    private readonly ILogger<ZipExtractionWorkflowStep> _logger;

    public ZipExtractionWorkflowStep(ILogger<ZipExtractionWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override string Name => "ZipExtraction";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state = context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        var zipFiles = FindZipFiles(state.RootPath).ToList();

        if (zipFiles.Count == 0)
        {
            _logger.LogInformation("No zip archives found — skipping ZipExtraction");
            return;
        }

        _logger.LogInformation("Found {Count} zip archive(s) to extract", zipFiles.Count);

        foreach (var zipPath in zipFiles)
        {
            await ExecuteOperationAsync(
                "ExtractZip",
                Path.GetRelativePath(state.RootPath, zipPath),
                () => ExtractAsync(zipPath, state.RootPath, cancellationToken),
                _logger);
        }
    }

    // Same skip list as DvdJoinWorkflowStep - never descend into recycle
    // bin/system folders or anything already archived by a prior QuickSort
    // step (.dvd-source, .zip-source itself).
    private static IEnumerable<string> FindZipFiles(string directory)
    {
        if (!Directory.Exists(directory)) yield break;

        var name = Path.GetFileName(directory);
        if (name.StartsWith('.') || name.StartsWith('@') || name.StartsWith('#') ||
            name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase))
            yield break;

        foreach (var file in Directory.EnumerateFiles(directory)
                     .Where(f => Path.GetExtension(f).Equals(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            yield return file;
        }

        foreach (var sub in Directory.EnumerateDirectories(directory))
        {
            foreach (var found in FindZipFiles(sub))
                yield return found;
        }
    }

    private Task ExtractAsync(string zipPath, string rootPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(zipPath)!;
        var targetName = Path.GetFileNameWithoutExtension(zipPath) + "_extracted";
        var targetPath = Path.Combine(directory, targetName);

        if (Directory.Exists(targetPath))
        {
            // Already extracted by a prior run (or by hand, matching the same
            // naming convention) - don't redo the work, just archive the zip
            // so it stops showing up in future scans.
            _logger.LogInformation("{Zip}: {Target} already exists, skipping extraction", RelativeName(zipPath, rootPath), targetName);
        }
        else
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                ZipFile.ExtractToDirectory(zipPath, targetPath);
                _logger.LogInformation("{Zip}: extracted → {Target}", RelativeName(zipPath, rootPath), targetName);
            }
            catch (Exception ex)
            {
                // A corrupt/partial zip shouldn't stop the rest of the
                // archives from extracting, and it should NOT be silently
                // archived away either - leave it in place, visible, so it
                // gets flagged for manual review instead of disappearing.
                _logger.LogWarning(ex, "{Zip}: extraction failed, leaving zip in place for manual review", RelativeName(zipPath, rootPath));
                if (Directory.Exists(targetPath))
                {
                    try { Directory.Delete(targetPath, recursive: true); } catch { /* best effort partial-extract cleanup */ }
                }
                return Task.CompletedTask;
            }
        }

        Archive(zipPath, rootPath);
        return Task.CompletedTask;
    }

    private void Archive(string zipPath, string rootPath)
    {
        var archiveRoot = Path.Combine(rootPath, ".zip-source");
        Directory.CreateDirectory(archiveRoot);

        var dest = Path.Combine(archiveRoot, Path.GetFileName(zipPath));
        if (File.Exists(dest))
        {
            dest = Path.Combine(archiveRoot, $"{Path.GetFileNameWithoutExtension(zipPath)}_{Guid.NewGuid():N}.zip");
        }

        File.Move(zipPath, dest);
        _logger.LogInformation("Archived {Zip} → .zip-source/", RelativeName(zipPath, rootPath));
    }

    private static string RelativeName(string path, string rootPath) =>
        Path.GetRelativePath(rootPath, path);
}
