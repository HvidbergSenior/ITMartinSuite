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
        AudioDuplicateDetectionWorkflowStep audioDuplicateDetectionWorkflowStep,
        ImageNormalizationWorkflowStep imageNormalizationWorkflowStep,
        VideoNormalizationWorkflowStep videoNormalizationWorkflowStep,
        VideoSegmentationWorkflowStep videoSegmentationWorkflowStep,
        SegmentThumbnailWorkflowStep segmentThumbnailWorkflowStep,
        CleanupEvaluationWorkflowStep cleanupEvaluationWorkflowStep,
        AiClassificationWorkflowStep aiClassificationWorkflowStep,
        Manifest1BuildWorkflowStep manifest1BuildWorkflowStep,
        ExportWorkflowExecutionStep exportWorkflowExecutionStep,
        ThumbnailWorkflowStep thumbnailWorkflowStep,
        GalleryThumbnailWorkflowStep galleryThumbnailWorkflowStep)
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

            audioDuplicateDetectionWorkflowStep,

            imageNormalizationWorkflowStep,

            videoNormalizationWorkflowStep,

            videoSegmentationWorkflowStep,

            segmentThumbnailWorkflowStep,

            cleanupEvaluationWorkflowStep,

            aiClassificationWorkflowStep,

            manifest1BuildWorkflowStep,

            exportWorkflowExecutionStep,

            // Runs against the final exported library, unlike the unused
            // thumbnailWorkflowStep above (which operates on source paths,
            // before export - the wrong stage, hence never enabled).
            galleryThumbnailWorkflowStep

            //thumbnailWorkflowStep
        ];
    }
}