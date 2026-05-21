using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class ResultMappingWorkflowStep
    : WorkflowStep<CardScanContext>
{
    public override string Name =>
        nameof(ResultMappingWorkflowStep);

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        context.State.Result =
            new CardScanResult
            {
                Name =
                    context.State.ScryfallMatchResult?.Name,

                SetCode =
                    context.State.ScryfallMatchResult?.SetCode,

                CollectorNumber =
                    context.State.ScryfallMatchResult?.CollectorNumber
            };

        await Task.CompletedTask;
    }
}