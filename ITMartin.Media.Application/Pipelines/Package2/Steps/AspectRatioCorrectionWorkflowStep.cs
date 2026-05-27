using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class AspectRatioCorrectionWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            AspectRatioCorrectionWorkflowStep>
        _logger;

    public override string Name =>
        nameof(AspectRatioCorrectionWorkflowStep);

    public AspectRatioCorrectionWorkflowStep(
        ILogger<
                AspectRatioCorrectionWorkflowStep>
            logger)
    {
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

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video))
        {
            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    _logger.LogInformation(
                        "Aspect ratio correction placeholder for {File}",
                        item.CurrentWorkingPath);

                    await Task.CompletedTask;
                },
                _logger);
        }
    }
}