using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;

public sealed class EnhancedThumbnailWorkflowStep
    : AnalogDigitizeWorkflowStepBase
{
    private readonly IThumbnailService
        _thumbnailService;

    private readonly ILogger<
            EnhancedThumbnailWorkflowStep>
        _logger;

    public override string Name =>
        nameof(EnhancedThumbnailWorkflowStep);

    public EnhancedThumbnailWorkflowStep(
        IThumbnailService thumbnailService,
        ILogger<EnhancedThumbnailWorkflowStep> logger)
    {
        _thumbnailService =
            thumbnailService;

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
                    x.CurrentWorkingPath is not null &&
                    x.ThumbnailOutputPath is not null &&
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

                    await _thumbnailService
                        .GenerateAsync(
                            item.CurrentWorkingPath!,
                            item.ThumbnailOutputPath!,
                            cts.Token);
                },
                _logger);
        }
    }
}