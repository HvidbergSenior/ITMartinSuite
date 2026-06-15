using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartinLibrary.Application.Workflows.Steps;

namespace ITMartinLibrary.Application.Workflows;

public sealed class ShelfScanWorkflowDefinition
    : IWorkflowDefinition
{
    public string Name =>
        "ShelfScanWorkflow";

    public WorkflowType WorkflowType =>
        WorkflowType.LibraryShelfScan;

    public IReadOnlyCollection<IWorkflowStep> Steps { get; }

    public ShelfScanWorkflowDefinition(
        AiShelfRecognitionWorkflowStep aiShelfRecognitionWorkflowStep,
        ItemLookupWorkflowStep itemLookupWorkflowStep,
        ShelfResultMappingWorkflowStep shelfResultMappingWorkflowStep)
    {
        Steps =
        [
            // Identify all visible items using AI vision
            aiShelfRecognitionWorkflowStep,

            // Enrich items with barcode/ISBN lookups
            itemLookupWorkflowStep,

            // Map to final result
            shelfResultMappingWorkflowStep
        ];
    }
}
