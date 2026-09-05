using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Runtime.BackgroundJobs;

public sealed class StartQuickSortHandler
    : IBackgroundJobHandler
{
    private readonly IScanOrchestrator _orchestrator;
    private readonly QuickSortWorkflowRunner _runner;
    private readonly IFaceIndexService _package3Service;
    private readonly ISmartFoldersService _smartFoldersService;
    private readonly ILibraryPolishService _libraryPolishService;
    private readonly IStaticGalleryExportService _staticGalleryExportService;
    private readonly ILibraryPathProvider _libraryPathProvider;
    private readonly ILogger<StartQuickSortHandler> _logger;

    public string JobType =>
        BackgroundJobTypes.StartQuickSort;

    public StartQuickSortHandler(
        IScanOrchestrator orchestrator,
        QuickSortWorkflowRunner runner,
        IFaceIndexService package3Service,
        ISmartFoldersService smartFoldersService,
        ILibraryPolishService libraryPolishService,
        IStaticGalleryExportService staticGalleryExportService,
        ILibraryPathProvider libraryPathProvider,
        ILogger<StartQuickSortHandler> logger)
    {
        _orchestrator = orchestrator;
        _runner = runner;
        _package3Service = package3Service;
        _smartFoldersService = smartFoldersService;
        _libraryPolishService = libraryPolishService;
        _staticGalleryExportService = staticGalleryExportService;
        _libraryPathProvider = libraryPathProvider;
        _logger = logger;
    }

    public async Task HandleAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        var request =
            JsonSerializer.Deserialize<
                QuickSortWorkflowState>(
                job.Payload);

        if (request is null)
        {
            return;
        }

        var workflowId =
            await _orchestrator.StartAsync(
                request,
                cancellationToken);

        await _runner.ExecuteAsync(
            workflowId,
            request,
            cancellationToken);

        // Same fallback ExportWorkflowExecutionStep itself uses - state.OutputPath
        // is only set when the caller passed one explicitly, otherwise the actual
        // export root is ILibraryPathProvider.LibraryRoot. Every add-on below has
        // to agree with export on where the files actually landed.
        var outputPath =
            !string.IsNullOrWhiteSpace(request.OutputPath)
                ? request.OutputPath
                : _libraryPathProvider.LibraryRoot;

        // Full mirror of QuickSort's own output, taken before any FaceIndex/
        // add-on step below gets a chance to touch it - see QuickSortBaselineHelper
        // for why. Refreshed on every run so it always reflects the latest
        // sort, not just the first one. A failure here shouldn't block the
        // add-on chain - the sort itself already succeeded either way, but
        // it does mean no safe rollback point exists for this run.
        if (request.EnableBaselineSnapshot)
        {
            try
            {
                await QuickSortBaselineHelper.MirrorDirectoryAsync(
                    outputPath,
                    QuickSortBaselineHelper.GetBaselinePath(outputPath),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Baseline snapshot failed for {OutputPath}", outputPath);
            }
        }

        // Free add-ons folded into the normal sort pass instead of being
        // separate, easy-to-forget catch-up steps run by hand via debug
        // endpoints - each is either free (no Claude API cost), local-only
        // ONNX (IndexFacesAsync), or cheap enough to treat as free by policy
        // (ClassifyUnhandledFilesAsync - text-only Haiku calls, capped at 500
        // files/run, ~$0.05/run). All are already incremental (skip work
        // already done), so a re-run against a mostly-unchanged library only
        // costs time for what's actually new. Each step is independently
        // try/caught so one failing step doesn't hide or block the others -
        // the sorted files are already safely in place regardless.
        //
        // FixOrientationFreeOnlyAsync used to run automatically here too -
        // removed 2026-09-03. Its face-detection heuristic only resolves a
        // rotation when a face is found at exactly one of 4 trial rotations;
        // any other photo (no face, or an ambiguous multi-rotation match -
        // most of a real library) came back unresolved and got quarantined
        // into RotationUkendt. Confirmed on Rico/AC's archive exactly like
        // the mie case documented at TryDetectOrientationViaFacesAsync's
        // other call site: hand-checked several hundred quarantined files,
        // none were actually rotated. Running a slow per-image face-detection
        // scan on every QuickSort pass for a signal this unreliable isn't
        // worth the cost - see ILibraryPolishService.FixOrientationFreeOnlyAsync
        // for the quarantine fix (kept for anyone still calling it directly),
        // but it's no longer part of the automatic pipeline.
        //
        // Everything still excluded here belongs to FaceIndex - the paid
        // features tier, kept manual/opt-in per CLAUDE.md's cost-discipline
        // rule because each makes real, non-trivial Claude API calls:
        //   - ILibraryPolishService.FixOrientationAsync (the paid Claude-vision
        //     fallback tier)
        //   - IImageTaggingService.TagLibraryAsync
        //   - ISmartFoldersService.EstimateUndatedPhotoYearsAsync
        //   - ISmartFoldersService.AddYearbookCaptionsAsync
        //   - ISmartFoldersService.PickBestShotsAsync
        // GenerateYearbookAsync also stays manual - it needs a specific year
        // chosen, which is a curatorial decision, not a mechanical cleanup step.
        // Runs first, before anything else reads pixels (IndexFaces' face
        // detection included) - free and deterministic, only acts when a
        // file already carries a real, non-ambiguous answer (a non-1 EXIF
        // Orientation tag), so there's no reason to make every downstream
        // pass work from a still-sideways image when this can resolve it
        // immediately. Previously only reachable via a manual debug
        // endpoint - the "easy, certain" cases were never actually applied
        // automatically despite costing nothing and risking nothing.
        await RunAddonStepAsync("BakeExifOrientation", outputPath,
            () => _libraryPolishService.BakeExifOrientationAsync(outputPath, cancellationToken));

        await RunAddonStepAsync("IndexFaces", outputPath,
            () => _package3Service.IndexFacesAsync(outputPath, cancellationToken: cancellationToken));

        // maxDatedReferenceFiles caps EstimateUndatedDatesAsync's own dated-
        // reference sampling (both the face and GPS passes) - without it, the
        // GPS pass reads EXIF/GPS from every single dated file in the library
        // one at a time with no bound at all (found 2026-08-25 on mie's real
        // library: ~43,000 files, looked hung for a long stretch - it wasn't,
        // just an unbounded serial scan). The IndexFaces step just above
        // already built the full-library reference set with no cap (that one
        // stays uncapped - it's the comprehensive baseline other FaceIndex
        // features rely on and isn't the thing that was ever unbounded-slow),
        // so this call's own internal IndexFacesAsync mostly just skip-fast
        // confirms that's already done.
        await RunAddonStepAsync("EstimateUndatedDates", outputPath,
            () => _package3Service.EstimateUndatedDatesAsync(outputPath, maxDatedReferenceFiles: 3000, cancellationToken: cancellationToken));

        await RunAddonStepAsync("LibraryPolish", outputPath,
            () => _libraryPolishService.PolishAsync(outputPath, cancellationToken));

        await RunAddonStepAsync("GenerateTripFolders", outputPath,
            () => _smartFoldersService.GenerateTripFoldersAsync(outputPath, cancellationToken));

        await RunAddonStepAsync("GenerateTraditions", outputPath,
            () => _smartFoldersService.GenerateTraditionsAsync(outputPath, cancellationToken));

        await RunAddonStepAsync("GenerateSimilarSceneFolders", outputPath,
            () => _smartFoldersService.GenerateSimilarSceneFoldersAsync(outputPath, cancellationToken));

        await RunAddonStepAsync("SyncGalleryCollections", outputPath,
            () => _smartFoldersService.SyncGalleryCollectionsAsync(outputPath, cancellationToken));

        await RunAddonStepAsync("StaticGalleryExport", outputPath,
            () => _staticGalleryExportService.ExportAsync(outputPath, cancellationToken));

        // Runs last - it classifies whatever FileDiscovery couldn't recognize
        // (dumped under Unhandled/), so it only makes sense after every other
        // step above has had a chance to move/consume files out of there.
        await RunAddonStepAsync("ClassifyUnhandled", outputPath,
            () => _package3Service.ClassifyUnhandledFilesAsync(outputPath, cancellationToken: cancellationToken));

        // Runs after ClassifyUnhandled for the same reason - clusters whatever
        // is genuinely still left in Undated/Unhandled once every other pass has
        // had its shot. Free/local (reuses IndexFaces's ONNX embeddings, no
        // Claude calls), so it belongs in this free tier, not the paid one below.
        await RunAddonStepAsync("GenerateUnknownPersonFolders", outputPath,
            () => _smartFoldersService.GenerateUnknownPersonFoldersAsync(outputPath, cancellationToken: cancellationToken));
    }

    private async Task RunAddonStepAsync(string stepName, string outputPath, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Post-sort add-on step {Step} failed for {OutputPath}", stepName, outputPath);
        }
    }
}