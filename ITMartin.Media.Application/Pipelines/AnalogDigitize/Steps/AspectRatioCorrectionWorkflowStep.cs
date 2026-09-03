using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;

public sealed class AspectRatioCorrectionWorkflowStep
    : AnalogDigitizeWorkflowStepBase
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
        if (context.State is not AnalogDigitizeWorkflowState state)
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