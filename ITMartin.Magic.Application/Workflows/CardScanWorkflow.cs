using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows;

public sealed class CardScanWorkflow
{
    private readonly IReadOnlyCollection<
        IWorkflowStep> _steps;

    public CardScanWorkflow(
        IReadOnlyCollection<
            IWorkflowStep> steps)
    {
        _steps = steps;
    }

    public async Task ExecuteAsync(
        CardScanContext context,
        CancellationToken cancellationToken)
    {
        foreach (var step in _steps)
        {
            var startedAt =
                DateTime.UtcNow;

            try
            {
                await step.ExecuteAsync(
                    context,
                    cancellationToken);

                context.Steps.Add(
                    new WorkflowExecutionStep
                    {
                        Name = step.GetType().Name,
                        Success = true,
                        Duration =
                            DateTime.UtcNow - startedAt
                    });

                if (context.Failed)
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                context.Fail(
                    exception.Message);

                context.Steps.Add(
                    new WorkflowExecutionStep
                    {
                        Name = step.GetType().Name,
                        Success = false,
                        Error = exception.Message,
                        Duration =
                            DateTime.UtcNow - startedAt
                    });

                return;
            }
        }
    }
}