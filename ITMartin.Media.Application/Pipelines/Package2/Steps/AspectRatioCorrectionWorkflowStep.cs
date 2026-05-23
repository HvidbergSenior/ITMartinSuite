using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AspectRatioCorrectionWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IImageEnhancementService
        _imageEnhancementService;

    private readonly ILogger<
            AspectRatioCorrectionWorkflowStep>
        _logger;

    public override string Name =>
        nameof(AspectRatioCorrectionWorkflowStep);

    public AspectRatioCorrectionWorkflowStep(
        IImageEnhancementService imageEnhancementService,
        ILogger<AspectRatioCorrectionWorkflowStep> logger)
    {
        _imageEnhancementService =
            imageEnhancementService;

        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Image &&
                         x.CurrentWorkingPath is not null &&
                         !x.Operations.Any(o =>
                             o.Name == Name &&
                             o.Success)))
        {
            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    _logger.LogInformation(
                        "START AspectRatioCorrection {File}",
                        item.CurrentWorkingPath);

                    using var cts =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                cancellationToken);

                    cts.CancelAfter(
                        TimeSpan.FromMinutes(5));

                    item.CurrentWorkingPath =
                        await _imageEnhancementService
                            .CorrectAspectRatioAsync(
                                item.CurrentWorkingPath!,
                                cts.Token);

                    _logger.LogInformation(
                        "END AspectRatioCorrection {File}",
                        item.CurrentWorkingPath);
                });
        }
    }
}