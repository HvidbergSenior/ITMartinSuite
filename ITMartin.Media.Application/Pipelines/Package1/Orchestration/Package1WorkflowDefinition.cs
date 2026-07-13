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
        DvdJoinWorkflowStep dvdJoinWorkflowStep,
        FileDiscoveryWorkflowStep fileDiscoveryWorkflowStep,
        MediaRulesWorkflowStep mediaRulesWorkflowStep,
        LivePhotoDetectionWorkflowStep livePhotoDetectionWorkflowStep,
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
            dvdJoinWorkflowStep,

            fileDiscoveryWorkflowStep,

            mediaRulesWorkflowStep,

            livePhotoDetectionWorkflowStep,

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