using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class ImageContrastWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IImageEnhancementService
        _imageEnhancementService;

    private readonly ILogger<
            ImageContrastWorkflowStep>
        _logger;

    public override string Name =>
        nameof(ImageContrastWorkflowStep);

    public ImageContrastWorkflowStep(
        IImageEnhancementService imageEnhancementService,
        ILogger<ImageContrastWorkflowStep> logger)
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
                        "START ImageContrast {File}",
                        item.CurrentWorkingPath);

                    using var cts =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                cancellationToken);

                    cts.CancelAfter(
                        TimeSpan.FromMinutes(5));

                    item.CurrentWorkingPath =
                        await _imageEnhancementService
                            .AdjustContrastAsync(
                                item.CurrentWorkingPath!,
                                cts.Token);

                    _logger.LogInformation(
                        "END ImageContrast {File}",
                        item.CurrentWorkingPath);
                });
        }
    }
}