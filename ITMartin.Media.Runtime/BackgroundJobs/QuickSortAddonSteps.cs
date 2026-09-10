using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Runtime.BackgroundJobs;

// The steps that run AFTER QuickSort's own 18, to finish a library in one go.
//
// TWO REASONS THIS TYPE EXISTS.
//
// 1. These used to live inline in StartQuickSortHandler, so they only ran
//    when a sort arrived through the job queue. A workflow picked up by
//    WorkflowRecoveryHostedService goes straight to the executor and never
//    touches that handler - so a recovered run completed all 18 sort steps,
//    reported success, and silently skipped everything below. That is exactly
//    what happened to ToshibaTest on 2026-09-09: the run was recovered after
//    a restart, finished cleanly, and left MediaFaces completely empty. Face
//    indexing had not failed, it was never invoked, and nothing in the logs
//    said so because nothing had gone wrong. Both paths now run the same set.
//
// 2. Only what is NECESSARY, CHEAP and SHORT runs automatically (user, 2026-09-10:
//    "I want all necessary easy and short steps to run in one go"). Everything
//    per-image-expensive or paid is deliberately NOT here - see the list at
//    the bottom. This pipeline has been bitten twice by a slow per-image pass
//    sitting in an automatic path: a rotation scan that managed ~600 of 38,367
//    images in four hours and held the whole delivery behind it, and an
//    ImageQuality step that spent 6m33s per run producing a verdict nothing
//    read.
public sealed class QuickSortAddonSteps
{
    private readonly ISmartFoldersService _smartFolders;
    private readonly ILibraryPolishService _polish;
    private readonly IStaticGalleryExportService _galleryExport;
    private readonly ILogger<QuickSortAddonSteps> _logger;

    public QuickSortAddonSteps(
        ISmartFoldersService smartFolders,
        ILibraryPolishService polish,
        IStaticGalleryExportService galleryExport,
        ILogger<QuickSortAddonSteps> logger)
    {
        _smartFolders = smartFolders;
        _polish = polish;
        _galleryExport = galleryExport;
        _logger = logger;
    }

    public async Task RunAsync(string outputPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || !Directory.Exists(outputPath))
        {
            _logger.LogWarning(
                "Skipping post-sort steps - no usable output path ({OutputPath})",
                outputPath);
            return;
        }

        _logger.LogInformation("Post-sort steps starting for {OutputPath}", outputPath);

        // Metadata-only and deterministic: acts only where a file already
        // carries a real, non-1 EXIF Orientation tag. Guesses nothing - the
        // photos it cannot resolve are staged for a human by
        // FileStatusWorkflowStep instead.
        await RunStepAsync("BakeExifOrientation", outputPath,
            () => _polish.BakeExifOrientationAsync(outputPath, cancellationToken));

        // Empty folders, OS junk, hidden manifest. Cheap directory work.
        await RunStepAsync("LibraryPolish", outputPath,
            () => _polish.PolishAsync(outputPath, cancellationToken));

        // Both read metadata already extracted during the sort - GPS for
        // trips, dates for Jul/Nytår - so they are fast regardless of library
        // size. On ToshibaTest: 4 trips and 2 traditions in seconds.
        await RunStepAsync("GenerateTripFolders", outputPath,
            () => _smartFolders.GenerateTripFoldersAsync(outputPath, cancellationToken));

        await RunStepAsync("GenerateTraditions", outputPath,
            () => _smartFolders.GenerateTraditionsAsync(outputPath, cancellationToken));

        await RunStepAsync("SyncGalleryCollections", outputPath,
            () => _smartFolders.SyncGalleryCollectionsAsync(outputPath, cancellationToken));

        // Last, and the one genuinely non-trivial step kept here: without it
        // the delivered drive has no index.html and nothing to browse, so it
        // is necessary rather than optional.
        await RunStepAsync("StaticGalleryExport", outputPath,
            () => _galleryExport.ExportAsync(outputPath, cancellationToken));

        _logger.LogInformation("Post-sort steps complete for {OutputPath}", outputPath);
    }

    // DELIBERATELY NOT RUN AUTOMATICALLY - each is either per-image expensive
    // or costs money. Run them explicitly against a delivered library when
    // they are actually wanted:
    //
    //   IndexFaces (IFaceIndexService.IndexFacesAsync)
    //     Extracts a face embedding from every image. Local and free, but
    //     per-image and slow on a real library. It is also the prerequisite
    //     for every person feature, so it wants to be a deliberate,
    //     measured run rather than a surprise tail on every sort.
    //
    //   EstimateUndatedDates (IFaceIndexService.EstimateUndatedDatesAsync)
    //     Face-matching plus a GPS pass; depends on the face index above, so
    //     it inherits its cost and is pointless without it.
    //
    //   GenerateUnknownPersonFolders (ISmartFoldersService)
    //     Reads the face index. Cheap on its own, but returns nothing at all
    //     until IndexFaces has run - on ToshibaTest it dutifully produced
    //     "0 unknown-person folders" because MediaFaces was empty.
    //
    //   ClassifyUnhandledFilesAsync (IFaceIndexService)
    //     Makes real Claude API calls. CLAUDE.md keeps paid passes
    //     manual/opt-in, and this one sat in the automatic path despite that.
    //
    //   GenerateSimilarSceneFolders - removed outright 2026-09-09
    //     ("IT IS NOT NEEDED - REMOVE"): copied perceptual-hash clusters into
    //     SmartFolders/Lignende, 605 MB of duplicated copies with poor
    //     grouping, and nothing in the delivered gallery ever linked it.

    // One failing step must not lose the rest - these run unattended after a
    // sort that may have taken hours.
    private async Task RunStepAsync(string stepName, string outputPath, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Post-sort step {Step} failed for {OutputPath}", stepName, outputPath);
        }
    }
}
