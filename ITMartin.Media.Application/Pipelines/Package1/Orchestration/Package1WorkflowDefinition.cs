using ITMartin.Media.Application.Pipelines.Package1.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package1.Orchestration;

public sealed class Package1WorkflowDefinition
    : IWorkflowDefinition
{
    public string Name =>
        "Package1Workflow";

    public IReadOnlyCollection<IWorkflowStep>
        Steps { get; }

    public Package1WorkflowDefinition(
        FileDiscoveryWorkflowStep fileDiscoveryWorkflowStep,
        HashWorkflowStep hashWorkflowStep,
        MetadataWorkflowStep metadataWorkflowStep,
        ImageNormalizationWorkflowStep imageNormalizationWorkflowStep,
        VideoNormalizationWorkflowStep videoNormalizationWorkflowStep,
        ThumbnailWorkflowStep thumbnailWorkflowStep,
        DuplicateDetectionWorkflowStep duplicateDetectionWorkflowStep,
        CleanupEvaluationWorkflowStep cleanupEvaluationWorkflowStep,
        ManifestBuildWorkflowStep manifestBuildWorkflowStep)
    {
        Steps =
        [
            fileDiscoveryWorkflowStep,

            hashWorkflowStep,

            metadataWorkflowStep,

            imageNormalizationWorkflowStep,

            videoNormalizationWorkflowStep,

            thumbnailWorkflowStep,

            duplicateDetectionWorkflowStep,

            cleanupEvaluationWorkflowStep,

            manifestBuildWorkflowStep
        ];
    }
}