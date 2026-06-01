using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Receipt.Application.Workflows;

namespace ITMartin.Receipt.Application.Services;

public sealed class ReceiptWorkflowRunner
{
    private readonly
        IWorkflowExecutor
        _workflowExecutor;

    private readonly
        ReceiptWorkflowDefinition
        _workflowDefinition;

    public ReceiptWorkflowRunner(
        IWorkflowExecutor workflowExecutor,
        ReceiptWorkflowDefinition workflowDefinition)
    {
        _workflowExecutor =
            workflowExecutor;

        _workflowDefinition =
            workflowDefinition;
    }

    public async Task<ReceiptContext>
        ExecuteAsync(
            string imagePath,
            CancellationToken cancellationToken)
    {
        var context =
            new ReceiptContext
            {
                ImagePath = imagePath
            };

        await _workflowExecutor
            .ExecuteAsync(
                _workflowDefinition,
                new WorkflowExecutionContext<
                    ReceiptContext>
                {
                    WorkflowId =
                        Guid.NewGuid(),

                    WorkflowName =
                        _workflowDefinition.Name,

                    State =
                        context,

                    CancellationToken =
                        cancellationToken
                },
                cancellationToken);

        return context;
    }
}