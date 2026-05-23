using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class ImageColorCorrectionWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IImageEnhancementService
        _imageEnhancementService;

    private readonly ILogger<
            ImageColorCorrectionWorkflowStep>
        _logger;

    public override string Name =>
        nameof(ImageColorCorrectionWorkflowStep);

    public ImageColorCorrectionWorkflowStep(
        IImageEnhancementService imageEnhancementService,
        ILogger<ImageColorCorrectionWorkflowStep> logger)
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
                        "START ImageColorCorrection {File}",
                        item.CurrentWorkingPath);

                    using var cts =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                cancellationToken);

                    cts.CancelAfter(
                        TimeSpan.FromMinutes(5));

                    item.CurrentWorkingPath =
                        await _imageEnhancementService
                            .ColorCorrectAsync(
                                item.CurrentWorkingPath!,
                                cts.Token);

                    _logger.LogInformation(
                        "END ImageColorCorrection {File}",
                        item.CurrentWorkingPath);
                });
        }
    }
}