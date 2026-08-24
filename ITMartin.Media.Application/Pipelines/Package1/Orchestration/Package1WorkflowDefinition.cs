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
        CleanStartWorkflowStep cleanStartWorkflowStep,
        DvdJoinWorkflowStep dvdJoinWorkflowStep,
        FileDiscoveryWorkflowStep fileDiscoveryWorkflowStep,
        MediaRulesWorkflowStep mediaRulesWorkflowStep,
        LivePhotoDetectionWorkflowStep livePhotoDetectionWorkflowStep,
        HashWorkflowStep hashWorkflowStep,
        MetadataWorkflowStep metadataWorkflowStep,
        DuplicateDetectionWorkflowStep duplicateDetectionWorkflowStep,
        AudioDuplicateDetectionWorkflowStep audioDuplicateDetectionWorkflowStep,
        ImageNormalizationWorkflowStep imageNormalizationWorkflowStep,
        ImageQualityWorkflowStep imageQualityWorkflowStep,
        VideoNormalizationWorkflowStep videoNormalizationWorkflowStep,
        CleanupEvaluationWorkflowStep cleanupEvaluationWorkflowStep,
        AiClassificationWorkflowStep aiClassificationWorkflowStep,
        Manifest1BuildWorkflowStep manifest1BuildWorkflowStep,
        ExportWorkflowExecutionStep exportWorkflowExecutionStep,
        GalleryThumbnailWorkflowStep galleryThumbnailWorkflowStep,
        FileStatusWorkflowStep fileStatusWorkflowStep)
    {
        Steps =
        [
            // Must run before anything else scans/reads the source folder -
            // see CleanStartWorkflowStep for why.
            cleanStartWorkflowStep,

            dvdJoinWorkflowStep,

            fileDiscoveryWorkflowStep,

            mediaRulesWorkflowStep,

            livePhotoDetectionWorkflowStep,

            hashWorkflowStep,

            metadataWorkflowStep,

            duplicateDetectionWorkflowStep,

            audioDuplicateDetectionWorkflowStep,

            imageNormalizationWorkflowStep,

            // Needs NormalizedPath (image side) already resolved - reads
            // whichever file the export will actually use.
            imageQualityWorkflowStep,

            videoNormalizationWorkflowStep,

            cleanupEvaluationWorkflowStep,

            aiClassificationWorkflowStep,

            manifest1BuildWorkflowStep,

            exportWorkflowExecutionStep,

            // Runs against the final exported library, post-export (the
            // pre-export ThumbnailWorkflowStep this superseded was removed
            // 2026-08-24 - dead code, never wired into this array).
            galleryThumbnailWorkflowStep,

            // Last - needs each file's final ExportedPath/category settled,
            // so every earlier step (classification, export routing) has
            // already run.
            fileStatusWorkflowStep
        ];
    }
}