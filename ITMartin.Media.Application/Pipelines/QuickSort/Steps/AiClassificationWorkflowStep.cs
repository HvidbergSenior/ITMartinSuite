using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

public sealed class AiClassificationWorkflowStep : QuickSortWorkflowStepBase
{
    // Hard ceiling on how many images get a real Claude call in one run - see
    // CLAUDE.md "AI/Claude API cost discipline". A library with more images
    // than this needs multiple imports/re-runs, on purpose - the previous
    // version of this step had no cap at all, which is exactly what that rule
    // exists to prevent.
    private const int MaxAiClassificationChecksPerRun = 2000;

    // Independent Claude calls in flight at once - same reasoning/value as
    // LibraryPolishService's ClassifyAiConcurrency and
    // ScreenshotReclassifyConcurrency (this call isn't batchable into one
    // prompt today - it returns a richer per-image verdict than the
    // rotation-fix's simple degrees-per-image - so concurrency is the lever
    // available here; batching would need a schema change to the underlying
    // Claude tool call, a separate piece of work).
    private const int AiClassificationConcurrency = 8;

    private readonly IImageAnalysisService _imageAnalysis;
    private readonly ILogger<AiClassificationWorkflowStep> _logger;

    public AiClassificationWorkflowStep(
        IImageAnalysisService imageAnalysis,
        ILogger<AiClassificationWorkflowStep> logger)
    {
        _imageAnalysis = imageAnalysis;
        _logger = logger;
    }

    public override string Name => "AiClassification";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state = context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        if (!state.EnableAiClassification)
        {
            _logger.LogInformation("AI classification disabled, skipping");
            return;
        }

        var allImages = state.MediaFiles
            .Where(f => f.Type == MediaType.Image &&
                        f.ExportSubFolder != "Duplicates" &&
                        f.ExportSubFolder != "DeleteCandidates")
            .ToList();

        var images = allImages.Take(MaxAiClassificationChecksPerRun).ToList();
        var skipped = allImages.Count - images.Count;
        if (skipped > 0)
        {
            _logger.LogWarning(
                "AI classification capped at {Cap} - {Skipped} images left unclassified this run",
                MaxAiClassificationChecksPerRun, skipped);
        }

        _logger.LogInformation("AI classifying {Count} images", images.Count);

        var total = images.Count;
        var done = 0;

        await ExecuteOperationAsync(
            "AiClassification",
            $"Images={total}",
            async () =>
            {
                await Parallel.ForEachAsync(
                    images,
                    new ParallelOptions { MaxDegreeOfParallelism = AiClassificationConcurrency, CancellationToken = cancellationToken },
                    async (file, ct) =>
                    {
                        var path = file.NormalizedPath ?? file.FullPath;

                        if (!File.Exists(path))
                        {
                            Interlocked.Increment(ref done);
                            return;
                        }

                        try
                        {
                            var result = await _imageAnalysis.AnalyzeImageAsync(path);

                            file.AiDescription = result.Description;
                            file.IsBlurry = result.IsBlurry;
                            file.IsSolidColor = result.IsSolidColor;

                            if (result.IsBlurry || result.IsSolidColor)
                            {
                                file.ExportSubFolder = "DeleteCandidates";
                                file.AiSubCategory = result.IsBlurry ? "Blurry" : "SolidColor";
                            }
                            else if (result.IsChat)
                            {
                                // More specific than plain IsScreenshot - a chat
                                // screenshot gets its own Chat category folder
                                // instead of sitting in Skærmbilleder.
                                file.SubCategory = MediaSubCategory.Chat;
                            }
                            else if (result.IsMeme)
                            {
                                file.SubCategory = MediaSubCategory.Meme;
                            }
                            else if (result.IsScreenshot && file.SubCategory != MediaSubCategory.Screenshot)
                            {
                                file.SubCategory = MediaSubCategory.Screenshot;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "AI classification failed for {File}", file.FileName);
                        }

                        var current = Interlocked.Increment(ref done);
                        LogStepProgress(_logger, Name, current, total, file.FileName);
                    });
            },
            _logger);
    }
}
