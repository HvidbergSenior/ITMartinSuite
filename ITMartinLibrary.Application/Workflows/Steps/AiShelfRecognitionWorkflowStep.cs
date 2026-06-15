using ITMartin.Ai.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartinLibrary.Application.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartinLibrary.Application.Workflows.Steps;

public sealed class AiShelfRecognitionWorkflowStep
    : WorkflowStep<ShelfScanContext>
{
    private readonly IOpenAiLibraryShelfRecognitionService _recognitionService;

    private readonly ILogger<AiShelfRecognitionWorkflowStep> _logger;

    public override string Name =>
        nameof(AiShelfRecognitionWorkflowStep);

    public AiShelfRecognitionWorkflowStep(
        IOpenAiLibraryShelfRecognitionService recognitionService,
        ILogger<AiShelfRecognitionWorkflowStep> logger)
    {
        _recognitionService = recognitionService;
        _logger = logger;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<ShelfScanContext> context,
        CancellationToken cancellationToken = default)
    {
        var result =
            await _recognitionService.AnalyzeAsync(
                context.State.ImagePath,
                cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException(
                "AI shelf recognition returned no result.");
        }

        context.State.AiResult = result;

        _logger.LogDebug(
            "AI shelf recognition — found {Count} items",
            result.Items.Count);
    }
}
