using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

// Twice now (2026-09-02 and 2026-09-04), files have ended up sitting
// directly under the shared library root (e.g. C:\FileSorter\library\
// Billeder\...) instead of inside the actual target library this run is
// writing to (e.g. C:\FileSorter\library\RicoAC\Billeder\...). Root cause is
// still unconfirmed - recovering from it used to mean noticing the stray
// folders by hand and running a one-off script. Runs early (right after
// CleanStart) so any earlier run's misplaced files are back where they
// belong before this run adds anything new - cheap (a top-level listing of
// the library root plus a hash compare per stray file), so no reason to
// skip it by default.
public sealed class LibraryRootReconcileWorkflowStep
    : QuickSortWorkflowStepBase
{
    // A genuine stray folder can only ever be named one of LibraryExportService's
    // own category folders (see its EnsureBaseFolders) - that's the exact set of
    // names the exporter itself creates, so a misplaced write can never produce
    // anything outside it. Critical: this must stay an allow-list, not a
    // deny-list. LibraryRoot commonly holds more than one customer's library
    // side by side (e.g. RicoAC and Mie as sibling folders) - an earlier version
    // of this step treated "any sibling directory that isn't the current
    // target" as stray, which would have classified a whole sibling customer's
    // library as one giant stray folder and merged its files into the wrong
    // customer's tree on the very next run against either library.
    private static readonly string[] KnownCategoryFolders =
        ["Billeder", "Videoer", "Dokumenter", "Musik", "Memes", "Gifs", "Film",
         "Chat", "Skærmbilleder", "LivePhotos", "SlettesKandidater", "Duplikater", "Ikke_identificeret"];

    private readonly ILibraryPathProvider _libraryPathProvider;
    private readonly ILogger<LibraryRootReconcileWorkflowStep> _logger;

    public LibraryRootReconcileWorkflowStep(
        ILibraryPathProvider libraryPathProvider,
        ILogger<LibraryRootReconcileWorkflowStep> logger)
    {
        _libraryPathProvider = libraryPathProvider;
        _logger = logger;
    }

    public override string Name => "LibraryRootReconcile";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state = context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        var libraryRoot = _libraryPathProvider.LibraryRoot;

        var targetLibrary = !string.IsNullOrWhiteSpace(state.OutputPath)
            ? state.OutputPath
            : libraryRoot;

        if (string.IsNullOrWhiteSpace(libraryRoot) || !Directory.Exists(libraryRoot))
            return;

        var targetFullPath = Path.GetFullPath(targetLibrary).TrimEnd(Path.DirectorySeparatorChar);
        var libraryRootFullPath = Path.GetFullPath(libraryRoot).TrimEnd(Path.DirectorySeparatorChar);

        // Nothing to reconcile against itself, and nothing to do if this run
        // writes straight to the shared root anyway.
        if (string.Equals(targetFullPath, libraryRootFullPath, StringComparison.OrdinalIgnoreCase))
            return;

        var strayDirs = Directory.GetDirectories(libraryRoot)
            .Where(d => !string.Equals(
                Path.GetFullPath(d).TrimEnd(Path.DirectorySeparatorChar),
                targetFullPath,
                StringComparison.OrdinalIgnoreCase))
            .Where(d => KnownCategoryFolders.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (strayDirs.Count == 0)
            return;

        await ExecuteOperationAsync(
            "LibraryRootReconcile",
            libraryRoot,
            async () =>
            {
                var moved = 0;
                var renamed = 0;
                var skippedDuplicate = 0;

                foreach (var strayDir in strayDirs)
                {
                    var category = Path.GetFileName(strayDir);

                    foreach (var srcFile in Directory.EnumerateFiles(strayDir, "*", SearchOption.AllDirectories))
                    {
                        if (Path.GetFileName(Path.GetDirectoryName(srcFile)!)
                            .Equals("thumbnails", StringComparison.OrdinalIgnoreCase))
                        {
                            // Disposable derived cache, not real content -
                            // GalleryThumbnailWorkflowStep regenerates these
                            // fresh against the correct location once the
                            // real file is moved back, so there's nothing
                            // worth carrying over.
                            File.Delete(srcFile);
                            continue;
                        }

                        var rel = Path.GetRelativePath(strayDir, srcFile);
                        var destFile = Path.Combine(targetLibrary, category, rel);

                        try
                        {
                            if (File.Exists(destFile))
                            {
                                if (FilesAreIdentical(srcFile, destFile))
                                {
                                    File.Delete(srcFile);
                                    skippedDuplicate++;
                                    continue;
                                }

                                // Same relative path, different content - old
                                // camera auto-numbered filenames collide this
                                // way for genuinely different photos (see
                                // [[project_ricoac_folder_polish]]). Keep
                                // both, never overwrite.
                                destFile = EnsureUniqueFileName(destFile);
                                renamed++;
                            }

                            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                            File.Move(srcFile, destFile);
                            moved++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "LibraryRootReconcile: failed to move {Src} -> {Dest}", srcFile, destFile);
                        }
                    }

                    RemoveEmptyDirectoriesRecursive(strayDir);
                }

                _logger.LogInformation(
                    """
                    LibraryRootReconcile completed
                    Moved: {Moved} (renamed on collision: {Renamed})
                    Skipped exact duplicates: {Skipped}
                    From: {Root}
                    Into: {Target}
                    """,
                    moved, renamed, skippedDuplicate, libraryRoot, targetLibrary);

                await Task.CompletedTask;
            },
            _logger);
    }

    private static bool FilesAreIdentical(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length)
            return false;

        return ComputeMd5(a).SequenceEqual(ComputeMd5(b));
    }

    private static byte[] ComputeMd5(string path)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(path);
        return md5.ComputeHash(stream);
    }

    // Matches LibraryExportService's own collision-naming convention
    // (name_2.ext, name_3.ext, ...) so recovered files read the same way as
    // any other naming collision this library already knows how to handle.
    private static string EnsureUniqueFileName(string path)
    {
        if (!File.Exists(path))
            return path;

        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var counter = 2;

        while (true)
        {
            var candidate = Path.Combine(directory, $"{name}_{counter}{ext}");
            if (!File.Exists(candidate))
                return candidate;
            counter++;
        }
    }

    private static void RemoveEmptyDirectoriesRecursive(string dir)
    {
        if (!Directory.Exists(dir))
            return;

        foreach (var sub in Directory.GetDirectories(dir))
            RemoveEmptyDirectoriesRecursive(sub);

        if (!Directory.EnumerateFileSystemEntries(dir).Any())
            Directory.Delete(dir);
    }
}
