using System.Text.RegularExpressions;
using ITMartin.Media.Application.Pipelines.LibraryFinishing;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Pipelines.FaceIndex;

// Requested 2026-09-07: "since running this could some of it be earlier" -
// the dependency-correct order for everything beyond QuickSort's own 19
// steps. Reorganization (rotation/reclassification/small-albums) must run
// BEFORE dedup and gallery-export, since those steps move or rewrite files -
// doing it after would mean re-exporting and re-pushing the gallery a
// second time to reflect the fix. See each phase's comment below for why it
// sits where it does.
//
// Every phase here is free/deterministic/no-human-input-required by design
// (see ILibraryFinishingService's own comment for exactly what's excluded
// and why) - safe to run unattended overnight against a real customer
// library with nobody watching.
public sealed class LibraryFinishingService : ILibraryFinishingService
{
    // Never real category folders - add-on/internal-bookkeeping folders this
    // pass must never touch as if they were a real collection to dedup.
    // Kept in sync by hand with LibraryPolishService's own ProtectedFolders
    // (that one isn't public - duplicating the literal names here is the
    // simplest correct option over exposing a whole new shared constant for
    // a handful of strings, per this codebase's own no-premature-abstraction
    // convention elsewhere).
    private static readonly HashSet<string> NonCategoryFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "_Galleri", "SmartFolders", "Review", "Ikke_identificeret",
        ".package1", ".package2", ".package3", ".ReferencePhotos",
        ".zip-source", ".dvd-source",
        LibraryPolishService.UnplayableFolderName,
        LibraryPolishService.RotationUnknownFolderName,
        LibraryPolishService.DuplicatesRemovedFolderName,
        LibraryPolishService.SmallAlbumsRemovedFolderName,
    };

    // Only these categories are actually Year/... bucketed - Musik,
    // Dokumenter, Skærmbilleder etc. aren't, so scanning them for "year
    // folders" would either find nothing or (worse) match some unrelated
    // 4-digit folder name by coincidence.
    private static readonly string[] YearBucketedCategories =
        ["Billeder", "Videoer", "Images", "Videos"];

    private static readonly Regex YearFolderPattern = new(@"^(19|20)\d{2}$", RegexOptions.Compiled);

    private readonly ILibraryPolishService _polish;
    private readonly ISmartFoldersService _smartFolders;
    private readonly IStaticGalleryExportService _galleryExport;
    private readonly ILibraryVerifyService _verify;
    private readonly ILogger<LibraryFinishingService> _logger;

    public LibraryFinishingService(
        ILibraryPolishService polish,
        ISmartFoldersService smartFolders,
        IStaticGalleryExportService galleryExport,
        ILibraryVerifyService verify,
        ILogger<LibraryFinishingService> logger)
    {
        _polish = polish;
        _smartFolders = smartFolders;
        _galleryExport = galleryExport;
        _verify = verify;
        _logger = logger;
    }

    // Real category folders directly under libraryPath - everything that
    // isn't a known add-on/bookkeeping folder and isn't dot-prefixed.
    // Public + static so this can be unit-tested directly without spinning
    // up the whole service.
    public static List<string> DiscoverRealCategoryFolders(string libraryPath)
    {
        if (!Directory.Exists(libraryPath)) return [];

        return Directory.EnumerateDirectories(libraryPath)
            .Where(d =>
            {
                var name = Path.GetFileName(d);
                return !name.StartsWith('.') && !NonCategoryFolders.Contains(name);
            })
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Every distinct year with its own Year folder under a year-bucketed
    // category - what GenerateYearbookAsync needs one call per year for.
    public static List<int> DiscoverYears(string libraryPath)
    {
        var years = new HashSet<int>();

        foreach (var category in YearBucketedCategories)
        {
            var categoryPath = Path.Combine(libraryPath, category);
            if (!Directory.Exists(categoryPath)) continue;

            foreach (var dir in Directory.EnumerateDirectories(categoryPath))
            {
                var name = Path.GetFileName(dir);
                if (YearFolderPattern.IsMatch(name) && int.TryParse(name, out var year))
                    years.Add(year);
            }
        }

        return years.OrderBy(y => y).ToList();
    }

    public async Task<LibraryFinishingReport> RunAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var report = new LibraryFinishingReport { LibraryPath = libraryPath };

        await RunPhaseAsync(report, "Reorganization", async () =>
        {
            // Reorganization first - these move/rewrite files, so anything
            // after this (index, dedup, gallery export) sees the final
            // structure instead of one that gets reshuffled later.
            report.OrientationFix = await _polish.FixOrientationFreeOnlyAsync(libraryPath, cancellationToken);
            report.BurstsFlattened = await _polish.FlattenBurstFoldersAsync(libraryPath, cancellationToken);
            report.AlbumArtReclassified = await _polish.ReclassifyAlbumArtAsync(libraryPath, cancellationToken);
            report.WebWatermarksReclassified = await _polish.ReclassifyWebWatermarksAsync(libraryPath, cancellationToken);
            report.SmallAlbumsPruned = await _polish.PruneSmallAlbumsAsync(libraryPath, cancellationToken: cancellationToken);
        });

        await RunPhaseAsync(report, "IndexConverge", async () =>
        {
            // maxAiCallsPerIteration: 0 - free/deterministic only, matching
            // every other phase here (see ILibraryFinishingService's comment
            // on why paid AI steps stay manual/opt-in).
            report.IndexReport = await _polish.RunUntilConvergedAsync(libraryPath, maxAiCallsPerIteration: 0, cancellationToken: cancellationToken);
            report.IndexConvergeIterations = 1;
        });

        await RunPhaseAsync(report, "Dedup", async () =>
        {
            // Now, not before reorganization - a file reclassified/moved by
            // phase 1 shouldn't dedup against its own pre-move location.
            var categoryFolders = DiscoverRealCategoryFolders(libraryPath);
            report.CategoryFoldersDeduped.AddRange(categoryFolders.Select(Path.GetFileName)!);

            foreach (var folder in categoryFolders)
            {
                var result = await _polish.DeduplicateFolderAsync(folder, cancellationToken);
                report.DuplicatesMoved += result.Deleted;
                report.FilesCheckedForDuplicates += result.Checked;
            }
        });

        await RunPhaseAsync(report, "SmartFoldersAddons", async () =>
        {
            // After dedup - these read the finalized structure, no point
            // building a Trip/Yearbook folder around a photo that's about to
            // be moved into Dubletter/ as a duplicate.
            report.Trips.AddRange(await _smartFolders.GenerateTripFoldersAsync(libraryPath, cancellationToken));
            report.Traditions.AddRange(await _smartFolders.GenerateTraditionsAsync(libraryPath, cancellationToken));

            foreach (var year in DiscoverYears(libraryPath))
            {
                var yearbook = await _smartFolders.GenerateYearbookAsync(libraryPath, year, cancellationToken);
                if (yearbook is not null) report.Yearbooks.Add(yearbook);
            }

            report.UnknownPersonFolders.AddRange(await _smartFolders.GenerateUnknownPersonFoldersAsync(libraryPath, cancellationToken: cancellationToken));
            report.SimilarSceneFolders.AddRange(await _smartFolders.GenerateSimilarSceneFoldersAsync(libraryPath, cancellationToken));

            await _smartFolders.SyncGalleryCollectionsAsync(libraryPath, cancellationToken);
            report.GalleryCollectionsSynced = true;
        });

        await RunPhaseAsync(report, "GalleryExport", async () =>
        {
            // Last of the content-generating phases - reflects correct
            // rotation, categories, dedup, and SmartFolders collections in
            // one export instead of needing a second pass to catch up.
            report.GalleryExport = await _galleryExport.ExportAsync(libraryPath, cancellationToken);
        });

        await RunPhaseAsync(report, "DeliveryVerify", async () =>
        {
            // Last overall - sanity-checks the truly final state.
            report.IntegrityReport = await _verify.VerifyLibraryAsync(libraryPath, cancellationToken);
            report.StructureReport = await _verify.VerifyStructureAsync(libraryPath, cancellationToken);
            report.CollectionsRepair = await _verify.RepairCollectionsPathsAsync(libraryPath, cancellationToken);
            report.DeliveryStructureReport = await _verify.VerifyDeliveryStructureAsync(libraryPath, cancellationToken);
        });

        report.FinishedAtUtc = DateTime.UtcNow;

        var markdown = LibraryFinishingReportFormatter.ToMarkdown(report);
        var reportPath = Path.Combine(libraryPath, "Kørselsrapport.md");
        try
        {
            await File.WriteAllTextAsync(reportPath, markdown, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write run report to {Path}", reportPath);
        }

        _logger.LogInformation(
            "LibraryFinishingService complete for {LibraryPath}: {Errors} phase(s) had errors, report at {ReportPath}",
            libraryPath, report.PhaseErrors.Count, reportPath);

        return report;
    }

    // One failing phase (a corrupt file, a transient AI/network error) must
    // not lose every other phase's real work for an overnight run nobody's
    // watching - logged and recorded in the report instead of thrown.
    private async Task RunPhaseAsync(LibraryFinishingReport report, string phaseName, Func<Task> phase)
    {
        try
        {
            await phase();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LibraryFinishingService phase {Phase} failed", phaseName);
            report.PhaseErrors.Add($"{phaseName}: {ex.Message}");
        }
    }
}
