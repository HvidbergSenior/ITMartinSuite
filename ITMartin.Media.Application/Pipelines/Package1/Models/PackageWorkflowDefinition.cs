using ITMartin.Media.Application.Pipelines.Package1.Steps;
using ITMartin.Media.Runtime.Interfaces;

namespace ITMartin.Media.Application.Pipelines.Package1.Models;

public sealed class Package1WorkflowDefinition
    : IWorkflowDefinition
{
    public string Name => "Package1Workflow";

    public IReadOnlyCollection<IWorkflowStep> Steps { get; }

    public Package1WorkflowDefinition(
        FileDiscoveryWorkflowStep fileDiscoveryWorkflowStep,
        HashWorkflowStep hashWorkflowStep,
        MetadataWorkflowStep metadataWorkflowStep)
    {
        Steps =
        [
            fileDiscoveryWorkflowStep,
            hashWorkflowStep,
            metadataWorkflowStep
        ];
    }
}