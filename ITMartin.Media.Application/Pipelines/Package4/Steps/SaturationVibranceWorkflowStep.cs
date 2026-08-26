using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

public sealed class SaturationVibranceWorkflowStep : Package2WorkflowStepBase
{
    private readonly ILogger<SaturationVibranceWorkflowStep> _logger;
    public override string Name => nameof(SaturationVibranceWorkflowStep);

    public SaturationVibranceWorkflowStep(ILogger<SaturationVibranceWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableSaturationVibrance)
        {
            _logger.LogInformation("Skipping saturation/vibrance boost");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.VideoFilters.Add("eq=saturation=1.3");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}
