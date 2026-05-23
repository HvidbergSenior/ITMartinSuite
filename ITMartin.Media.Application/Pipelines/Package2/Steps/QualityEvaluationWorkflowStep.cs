using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class QualityEvaluationWorkflowStep
    : Package2WorkflowStepBase
{
    public override string Name =>
        nameof(QualityEvaluationWorkflowStep);

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
                         !x.Operations.Any(o =>
                             o.Name == Name &&
                             o.Success)))
        {
            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    var fileInfo =
                        new FileInfo(
                            item.CurrentWorkingPath!);

                    if (!fileInfo.Exists)
                    {
                        throw new InvalidOperationException(
                            "Enhanced file missing.");
                    }

                    await Task.CompletedTask;
                });
        }
    }
}