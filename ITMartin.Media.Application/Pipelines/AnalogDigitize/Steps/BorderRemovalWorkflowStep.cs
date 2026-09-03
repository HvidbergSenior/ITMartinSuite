using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;

public sealed class BorderRemovalWorkflowStep
    : AnalogDigitizeWorkflowStepBase
{
    private readonly IImageEnhancementService
        _imageEnhancementService;

    private readonly ILogger<
            BorderRemovalWorkflowStep>
        _logger;

    public override string Name =>
        nameof(BorderRemovalWorkflowStep);

    public BorderRemovalWorkflowStep(
        IImageEnhancementService imageEnhancementService,
        ILogger<BorderRemovalWorkflowStep> logger)
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
        if (context.State is not AnalogDigitizeWorkflowState state)
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
                    using var cts =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                cancellationToken);

                    cts.CancelAfter(
                        TimeSpan.FromMinutes(5));

                    item.CurrentWorkingPath =
                        await _imageEnhancementService
                            .RemoveBordersAsync(
                                item.CurrentWorkingPath!,
                                cts.Token);
                },
                _logger);
        }
    }
}