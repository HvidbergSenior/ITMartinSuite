using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class AiClassificationWorkflowStep : Package1WorkflowStepBase
{
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
        var state = context.State as Package1WorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        if (!state.EnableAiClassification)
        {
            _logger.LogInformation("AI classification disabled, skipping");
            return;
        }

        var images = state.MediaFiles
            .Where(f => f.Type == MediaType.Image &&
                        f.ExportSubFolder != "Duplicates" &&
                        f.ExportSubFolder != "DeleteCandidates")
            .ToList();

        _logger.LogInformation("AI classifying {Count} images", images.Count);

        var total = images.Count;
        var done = 0;

        await ExecuteOperationAsync(
            "AiClassification",
            $"Images={total}",
            async () =>
            {
                foreach (var file in images)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var path = file.NormalizedPath ?? file.FullPath;

                    if (!File.Exists(path))
                    {
                        done++;
                        continue;
                    }

                    try
                    {
                        var result = await _imageAnalysis.AnalyzeImageAsync(path);

                        file.AiDescription = result.Description;

                        if (result.IsBlurry || result.IsSolidColor)
                        {
                            file.ExportSubFolder = "DeleteCandidates";
                            file.AiSubCategory = result.IsBlurry ? "Blurry" : "SolidColor";
                        }
                        else if (result.IsMeme && file.SubCategory != MediaSubCategory.Meme)
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

                    done++;
                    LogStepProgress(_logger, Name, done, total, file.FileName);
                }
            },
            _logger);
    }
}
