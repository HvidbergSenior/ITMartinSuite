using ITMartin.Media.Application.Pipelines.Package1.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package1.Orchestration;

public sealed class Package1WorkflowDefinition
    : IWorkflowDefinition
{
    public string Name =>
        "Package1Workflow";
    public WorkflowType WorkflowType =>
        WorkflowType.Package1;
    public IReadOnlyCollection<IWorkflowStep>
        Steps { get; }

    public Package1WorkflowDefinition(
        FileDiscoveryWorkflowStep fileDiscoveryWorkflowStep,
        MediaRulesWorkflowStep mediaRulesWorkflowStep,
        HashWorkflowStep hashWorkflowStep,
        MetadataWorkflowStep metadataWorkflowStep,
        DuplicateDetectionWorkflowStep duplicateDetectionWorkflowStep,
        ImageNormalizationWorkflowStep imageNormalizationWorkflowStep,
        VideoNormalizationWorkflowStep videoNormalizationWorkflowStep,
        VideoSegmentationWorkflowStep videoSegmentationWorkflowStep,
        SegmentThumbnailWorkflowStep segmentThumbnailWorkflowStep,
        CleanupEvaluationWorkflowStep cleanupEvaluationWorkflowStep,
        AiClassificationWorkflowStep aiClassificationWorkflowStep,
        Manifest1BuildWorkflowStep manifest1BuildWorkflowStep,
        ExportWorkflowExecutionStep exportWorkflowExecutionStep,
        ThumbnailWorkflowStep thumbnailWorkflowStep)
    {
        Steps =
        [
            fileDiscoveryWorkflowStep,

            mediaRulesWorkflowStep,

            hashWorkflowStep,

            metadataWorkflowStep,

            duplicateDetectionWorkflowStep,

            imageNormalizationWorkflowStep,

            videoNormalizationWorkflowStep,

            videoSegmentationWorkflowStep,

            segmentThumbnailWorkflowStep,

            cleanupEvaluationWorkflowStep,

            aiClassificationWorkflowStep,

            manifest1BuildWorkflowStep,

            exportWorkflowExecutionStep

            //thumbnailWorkflowStep
        ];
    }
}