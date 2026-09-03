using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

public sealed class DeEssWorkflowStep : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<DeEssWorkflowStep> _logger;
    public override string Name => nameof(DeEssWorkflowStep);

    public DeEssWorkflowStep(ILogger<DeEssWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableDeEss)
        {
            _logger.LogInformation("Skipping de-essing");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.AudioFilters.Add("deesser");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}
