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

    // Images per Claude call - was one call per image with 8-way concurrency
    // (concurrency, not batching); per CLAUDE.md "AI/Claude API cost
    // discipline", concurrency alone burns through budget faster, it doesn't
    // reduce it - batching multiple images into one call is the actual fix,
    // via IImageAnalysisService.AnalyzeImagesBatchAsync's array-of-results
    // tool schema. 8 keeps a single call's image-token payload reasonable
    // while cutting per-call fixed overhead (system prompt, tool schema) by
    // ~8x versus one-call-per-image.
    private const int ImagesPerBatch = 8;

    // Batches in flight at once - bounds total concurrent image-token
    // payload in flight (BatchConcurrency * ImagesPerBatch images), not
    // per-image concurrency.
    private const int BatchConcurrency = 3;

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
                var batches = images
                    .Select((file, i) => (file, i))
                    .GroupBy(x => x.i / ImagesPerBatch)
                    .Select(g => g.Select(x => x.file).ToList())
                    .ToList();

                await Parallel.ForEachAsync(
                    batches,
                    new ParallelOptions { MaxDegreeOfParallelism = BatchConcurrency, CancellationToken = cancellationToken },
                    async (batch, ct) =>
                    {
                        var existing = batch
                            .Select(file => file.NormalizedPath ?? file.FullPath)
                            .ToList();
                        var validPairs = batch.Zip(existing, (file, path) => (file, path))
                            .Where(p => File.Exists(p.path))
                            .ToList();

                        if (validPairs.Count == 0)
                        {
                            Interlocked.Add(ref done, batch.Count);
                            return;
                        }

                        IReadOnlyList<AiAnalysisResult> results;
                        try
                        {
                            results = await _imageAnalysis.AnalyzeImagesBatchAsync(
                                validPairs.Select(p => p.path).ToList());
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "AI classification batch of {Count} failed", validPairs.Count);
                            Interlocked.Add(ref done, batch.Count);
                            return;
                        }

                        for (var i = 0; i < validPairs.Count; i++)
                        {
                            var file = validPairs[i].file;
                            var result = results[i];

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

                        var current = Interlocked.Add(ref done, batch.Count);
                        LogStepProgress(_logger, Name, current, total, $"batch of {validPairs.Count}");
                    });
            },
            _logger);
    }
}
