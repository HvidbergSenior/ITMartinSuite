using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Receipt.Application.Workflows.Steps;

namespace ITMartin.Receipt.Application.Workflows;

public sealed class ReceiptWorkflowDefinition
    : IWorkflowDefinition
{
    public string Name =>
        "ReceiptWorkflow";

    public WorkflowType WorkflowType =>
        WorkflowType.ReceiptScan;

    public IReadOnlyCollection<IWorkflowStep>
        Steps { get; }

    public ReceiptWorkflowDefinition(
        ReceiptOcrWorkflowStep receiptOcrWorkflowStep,
        OpenAiReceiptExtractionWorkflowStep openAiReceiptExtractionWorkflowStep,
        SaveTransactionWorkflowStep saveTransactionWorkflowStep)
    {
        Steps =
        [
            // Extract text
            receiptOcrWorkflowStep,

            // Extract receipt data
            openAiReceiptExtractionWorkflowStep,

            // Persist result
            saveTransactionWorkflowStep
        ];
    }
}