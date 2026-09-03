using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.AnalogDigitize.Orchestration;

public sealed class AnalogDigitizeWorkflowRunner
{
    private readonly IWorkflowExecutor
        _workflowExecutor;

    private readonly AnalogDigitizeWorkflowDefinition
        _workflowDefinition;

    public AnalogDigitizeWorkflowRunner(
        IWorkflowExecutor workflowExecutor,
        AnalogDigitizeWorkflowDefinition workflowDefinition)
    {
        _workflowExecutor =
            workflowExecutor;

        _workflowDefinition =
            workflowDefinition;
    }

    public async Task ExecuteAsync(
        Guid workflowId,
        AnalogDigitizeWorkflowState state,
        CancellationToken cancellationToken)
    {
        await _workflowExecutor.ExecuteAsync(
            _workflowDefinition,
            new WorkflowExecutionContext<
                AnalogDigitizeWorkflowState>
            {
                WorkflowId =
                    workflowId,

                WorkflowName =
                    _workflowDefinition.Name,

                State =
                    state,

                CancellationToken =
                    cancellationToken
            },
            cancellationToken);
    }
}