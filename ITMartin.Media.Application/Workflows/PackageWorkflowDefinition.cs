using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Workflows.Steps;

namespace ITMartin.Media.Application.Workflows;

public sealed class Package1WorkflowDefinition
    : IWorkflowDefinition
{
    public string Name => "Package1Workflow";

    public IReadOnlyCollection<IWorkflowStep> Steps { get; }

    public Package1WorkflowDefinition(
        FileDiscoveryWorkflowStep fileDiscoveryWorkflowStep,
        HashWorkflowStep hashWorkflowStep,
        MetadataWorkflowStep metadataWorkflowStep,
        CrashWorkflowStep crashWorkflowStep)
    {
        Steps =
        [
            fileDiscoveryWorkflowStep,
            hashWorkflowStep,
            metadataWorkflowStep,
            crashWorkflowStep
        ];
    }
}