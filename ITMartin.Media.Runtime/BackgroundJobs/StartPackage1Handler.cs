using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Runtime.BackgroundJobs;

public sealed class StartPackage1Handler
    : IBackgroundJobHandler
{
    private readonly IScanOrchestrator _orchestrator;
    private readonly Package1WorkflowRunner _runner;
    private readonly IPackage3Service _package3Service;
    private readonly ISmartFoldersService _smartFoldersService;
    private readonly ILibraryPolishService _libraryPolishService;
    private readonly IStaticGalleryExportService _staticGalleryExportService;
    private readonly ILibraryPathProvider _libraryPathProvider;
    private readonly ILogger<StartPackage1Handler> _logger;

    public string JobType =>
        BackgroundJobTypes.StartPackage1;

    public StartPackage1Handler(
        IScanOrchestrator orchestrator,
        Package1WorkflowRunner runner,
        IPackage3Service package3Service,
        ISmartFoldersService smartFoldersService,
        ILibraryPolishService libraryPolishService,
        IStaticGalleryExportService staticGalleryExportService,
        ILibraryPathProvider libraryPathProvider,
        ILogger<StartPackage1Handler> logger)
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
                Package1WorkflowState>(
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

        // Full mirror of Package1's own output, taken before any Package3/
        // add-on step below gets a chance to touch it - see Package1BaselineHelper
        // for why. Refreshed on every run so it always reflects the latest
        // sort, not just the first one. A failure here shouldn't block the
        // add-on chain - the sort itself already succeeded either way, but
        // it does mean no safe rollback point exists for this run.
        if (request.EnableBaselineSnapshot)
        {
            try
            {
                await Package1BaselineHelper.MirrorDirectoryAsync(
                    outputPath,
                    Package1BaselineHelper.GetBaselinePath(outputPath),
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
        // Everything still excluded here belongs to Package3 - the paid
        // features tier, kept manual/opt-in per CLAUDE.md's cost-discipline
        // rule because each makes real, non-trivial Claude API calls:
        //   - ILibraryPolishService.FixOrientationAsync (rotation, vision calls)
        //   - IImageTaggingService.TagLibraryAsync
        //   - ISmartFoldersService.EstimateUndatedPhotoYearsAsync
        //   - ISmartFoldersService.AddYearbookCaptionsAsync
        //   - ISmartFoldersService.PickBestShotsAsync
        // GenerateYearbookAsync also stays manual - it needs a specific year
        // chosen, which is a curatorial decision, not a mechanical cleanup step.
        await RunAddonStepAsync("IndexFaces", outputPath,
            () => _package3Service.IndexFacesAsync(outputPath, cancellationToken));

        await RunAddonStepAsync("EstimateUndatedDates", outputPath,
            () => _package3Service.EstimateUndatedDatesAsync(outputPath, cancellationToken: cancellationToken));

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