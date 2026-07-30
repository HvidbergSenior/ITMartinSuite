using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Receipt.Application.Interfaces;

namespace ITMartin.Receipt.Application.Workflows;

public sealed class ReceiptWorkflowOrchestrator : IReceiptWorkflowOrchestrator
{
    private readonly
        IWorkflowExecutor
        _workflowExecutor;

    private readonly
        ReceiptWorkflowDefinition
        _workflowDefinition;

    public ReceiptWorkflowOrchestrator(
        IWorkflowExecutor workflowExecutor,
        ReceiptWorkflowDefinition workflowDefinition)
    {
        _workflowExecutor =
            workflowExecutor;

        _workflowDefinition =
            workflowDefinition;
    }

    public async Task<ReceiptContext> ExecuteAsync(
        string imagePath,
        CancellationToken cancellationToken,
        string? itemsPhotoPath = null,
        List<string>? additionalImagePaths = null)
    {
        var context =
            new ReceiptContext
            {
                ImagePath = imagePath,
                ItemsPhotoPath = itemsPhotoPath,
                AdditionalImagePaths = additionalImagePaths ?? []
            };

        await _workflowExecutor.ExecuteAsync(
            _workflowDefinition,
            new WorkflowExecutionContext<ReceiptContext>
            {
                WorkflowId = Guid.NewGuid(),
                WorkflowName = _workflowDefinition.Name,
                State = context,
                CancellationToken = cancellationToken
            },
            cancellationToken);

        return context;
    }
}