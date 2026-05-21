using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows;

public sealed class CardScanWorkflow
{
    private readonly
        IReadOnlyCollection<IWorkflowStep>
        _steps;

    public CardScanWorkflow(
        CardScanWorkflowDefinition workflowDefinition)
    {
        _steps =
            workflowDefinition.Steps;
    }

    public async Task ExecuteAsync(
        CardScanContext context,
        CancellationToken cancellationToken)
    {
        var workflowContext =
            new WorkflowExecutionContext<CardScanContext>
            {
                WorkflowId =
                    Guid.NewGuid(),

                WorkflowName =
                    nameof(CardScanWorkflow),

                State =
                    context,

                CancellationToken =
                    cancellationToken
            };

        foreach (var step in _steps)
        {
            var startedAt =
                DateTime.UtcNow;

            try
            {
                Console.WriteLine(
                    $"Executing workflow step: {step.Name}");

                await step.ExecuteAsync(
                    workflowContext,
                    cancellationToken);

                context.Steps.Add(
                    new WorkflowExecutionStep
                    {
                        Name =
                            step.Name,

                        Success =
                            true,

                        Duration =
                            DateTime.UtcNow - startedAt
                    });
            }
            catch (Exception exception)
            {
                context.Fail(
                    exception.Message);

                context.Steps.Add(
                    new WorkflowExecutionStep
                    {
                        Name =
                            step.Name,

                        Success =
                            false,

                        Error =
                            exception.Message,

                        Duration =
                            DateTime.UtcNow - startedAt
                    });

                return;
            }
        }
    }
}