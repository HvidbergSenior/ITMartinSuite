using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// Fixes brightness flicker from indoor artificial lighting (fluorescent/LED
// mains-frequency beat against the phone's rolling shutter) - common in
// indoor phone footage, rare outdoors.
public sealed class DeflickerWorkflowStep : Package2WorkflowStepBase
{
    private readonly ILogger<DeflickerWorkflowStep> _logger;
    public override string Name => nameof(DeflickerWorkflowStep);

    public DeflickerWorkflowStep(ILogger<DeflickerWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableDeflicker)
        {
            _logger.LogInformation("Skipping deflicker");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.VideoFilters.Add("deflicker=mode=pm");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}
