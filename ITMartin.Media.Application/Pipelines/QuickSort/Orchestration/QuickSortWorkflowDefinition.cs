using ITMartin.Media.Application.Pipelines.QuickSort.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;

public sealed class QuickSortWorkflowDefinition
    : IWorkflowDefinition
{
    public string Name =>
        "QuickSortWorkflow";
    public WorkflowType WorkflowType =>
        WorkflowType.QuickSort;
    public IReadOnlyCollection<IWorkflowStep>
        Steps { get; }

    public QuickSortWorkflowDefinition(
        CleanStartWorkflowStep cleanStartWorkflowStep,
        LibraryRootReconcileWorkflowStep libraryRootReconcileWorkflowStep,
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
        CleanupEvaluationWorkflowStep cleanupEvaluationWorkflowStep,
        AiClassificationWorkflowStep aiClassificationWorkflowStep,
        Manifest1BuildWorkflowStep manifest1BuildWorkflowStep,
        ExportWorkflowExecutionStep exportWorkflowExecutionStep,
        VideoConvertFinalizeWorkflowStep videoConvertFinalizeWorkflowStep,
        GalleryThumbnailWorkflowStep galleryThumbnailWorkflowStep,
        FileStatusWorkflowStep fileStatusWorkflowStep)
    {
        Steps =
        [
            // Must run before anything else scans/reads the source folder -
            // see CleanStartWorkflowStep for why.
            cleanStartWorkflowStep,

            // Also early - reconciles the shared library root before this
            // run adds anything new to it. Independent of the source scan
            // above (this looks at the destination, not RootPath), so order
            // relative to CleanStart doesn't matter beyond "both early."
            libraryRootReconcileWorkflowStep,

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

            cleanupEvaluationWorkflowStep,

            aiClassificationWorkflowStep,

            manifest1BuildWorkflowStep,

            exportWorkflowExecutionStep,

            // Videos were dispatched for conversion back in
            // MediaRulesWorkflowStep (step 4) - by now some have already
            // finished and got exported pre-converted, this catches the
            // rest and swaps them in once each one's conversion completes
            // (fire-and-forget, doesn't block QuickSort's own completion).
            videoConvertFinalizeWorkflowStep,

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