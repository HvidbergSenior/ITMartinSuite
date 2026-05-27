using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class ImageUpscaleWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IImageEnhancementService
        _imageEnhancementService;

    private readonly ILogger<
            ImageUpscaleWorkflowStep>
        _logger;

    public override string Name =>
        nameof(ImageUpscaleWorkflowStep);

    public ImageUpscaleWorkflowStep(
        IImageEnhancementService imageEnhancementService,
        ILogger<ImageUpscaleWorkflowStep> logger)
    {
        _imageEnhancementService =
            imageEnhancementService;

        _logger =
            logger;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        var items =
            state.Items
                .Where(x =>
                    !x.Failed &&
                    x.MediaKind == MediaKind.Image &&
                    x.CurrentWorkingPath is not null &&
                    !AlreadyExecuted(x, Name))
                .ToList();

        var total =
            items.Count;

        var current = 0;

        foreach (var item in items)
        {
            current++;

            _logger.LogInformation(
                "[{Step}] {Current}/{Total} {File}",
                Name,
                current,
                total,
                item.CurrentWorkingPath);

            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    using var cts =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                cancellationToken);

                    cts.CancelAfter(
                        TimeSpan.FromMinutes(10));

                    item.CurrentWorkingPath =
                        await _imageEnhancementService
                            .UpscaleAsync(
                                item.CurrentWorkingPath!,
                                cts.Token);

                    cancellationToken
                        .ThrowIfCancellationRequested();
                },
                _logger);
        }
    }
}