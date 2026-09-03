using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;

public sealed class VideoCropWorkflowStep
    : IWorkflowStep
{
    private readonly ILogger<
            VideoCropWorkflowStep>
        _logger;

    public string Name =>
        nameof(VideoCropWorkflowStep);

    public VideoCropWorkflowStep(
        ILogger<VideoCropWorkflowStep> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        if (context.State is not AnalogDigitizeWorkflowState state)
        {
            return Task.CompletedTask;
        }

        if (!state.EnableCrop)
        {
            _logger.LogInformation(
                "Skipping crop");

            return Task.CompletedTask;
        }

        const int bottomCrop = 120;

        var filter =
            $"crop=in_w:in_h-{bottomCrop}:0:0";

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video))
        {
            item.VideoFilters.Add(
                filter);

            _logger.LogInformation(
                """
                Added crop filter
                Item: {Item}
                Filter: {Filter}
                """,
                item.CurrentWorkingPath,
                filter);
        }

        return Task.CompletedTask;
    }
}