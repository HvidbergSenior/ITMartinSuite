using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class EnhancedThumbnailWorkflowStep
    : Package2WorkflowStepBase
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
                         x.CurrentWorkingPath is not null &&
                         x.ThumbnailOutputPath is not null &&
                         !AlreadyExecuted(x, Name)))
        {
            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    _logger.LogInformation(
                        "START EnhancedThumbnail {File}",
                        item.CurrentWorkingPath);

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

                    _logger.LogInformation(
                        "END EnhancedThumbnail {File}",
                        item.CurrentWorkingPath);
                });
        }
    }
}