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
        StoragePreflightWorkflowStep storagePreflightWorkflowStep,
        CleanStartWorkflowStep cleanStartWorkflowStep,
        LibraryRootReconcileWorkflowStep libraryRootReconcileWorkflowStep,
        DvdJoinWorkflowStep dvdJoinWorkflowStep,
        ZipExtractionWorkflowStep zipExtractionWorkflowStep,
        FileDiscoveryWorkflowStep fileDiscoveryWorkflowStep,
        MediaRulesWorkflowStep mediaRulesWorkflowStep,
        HashWorkflowStep hashWorkflowStep,
        MetadataWorkflowStep metadataWorkflowStep,
        DuplicateDetectionWorkflowStep duplicateDetectionWorkflowStep,
        AudioDuplicateDetectionWorkflowStep audioDuplicateDetectionWorkflowStep,
        ImageNormalizationWorkflowStep imageNormalizationWorkflowStep,
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
            // Absolutely first. Proves the source and destination are really
            // there, really respond, and really have content before any other
            // step reads or writes a single byte - and in particular before
            // CleanStart below, which DELETES generated folders from the
            // source and must never run against a path whose mount is stale.
            // See StoragePreflightWorkflowStep for the drive-drop history that
            // put this here.
            storagePreflightWorkflowStep,

            // Must run before anything else scans/reads the source folder -
            // see CleanStartWorkflowStep for why.
            cleanStartWorkflowStep,

            // Also early - reconciles the shared library root before this
            // run adds anything new to it. Independent of the source scan
            // above (this looks at the destination, not RootPath), so order
            // relative to CleanStart doesn't matter beyond "both early."
            libraryRootReconcileWorkflowStep,

            dvdJoinWorkflowStep,

            // Also before FileDiscovery - extracts any .zip archives sitting
            // in the raw source (common in whole-drive backup dumps: iCloud/
            // Dropbox export zips) so their contents get scanned as real
            // files instead of the zip itself being carried through as one
            // opaque "Other" file. See ZipExtractionWorkflowStep.
            zipExtractionWorkflowStep,

            fileDiscoveryWorkflowStep,

            mediaRulesWorkflowStep,

            // LivePhotoDetectionWorkflowStep removed from the pipeline
            // 2026-09-08 ("the important thing is categorizing" - basic
            // MediaClassification above is what matters; a Live Photo's
            // companion video just flows through as an ordinary video
            // instead of getting its own pairing-detection pass). "LivePhotos"
            // stays in CategoryHelper's never-necessary set defensively, but
            // nothing sets that SubCategory anymore now that this step is
            // gone.

            hashWorkflowStep,

            metadataWorkflowStep,

            duplicateDetectionWorkflowStep,

            audioDuplicateDetectionWorkflowStep,

            // Moved here 2026-09-07 ("get all not relevant files off as fast
            // as possible... so that this does not happen" - live on a real
            // 84,535-file/77,061-image run, ImageNormalization was
            // normalizing exact duplicates that get thrown away two steps
            // later anyway). Every decision this step makes only needs data
            // already available by here: DuplicateGroups (from
            // DuplicateDetection/AudioDuplicateDetection, just above),
            // Width/Height/Duration/Artist (from MetadataWorkflowStep, step
            // 8 - confirmed 2026-09-07, nothing it reads comes from
            // ImageNormalization/ImageQuality). Files it marks
            // Duplicates/DeleteCandidates are now skipped by both of those
            // expensive per-image steps instead of being processed and
            // thrown away.
            cleanupEvaluationWorkflowStep,

            imageNormalizationWorkflowStep,

            // ImageQualityWorkflowStep removed 2026-09-09. It spent 6m33s per
            // run decoding every image to set IsBlurry/IsSolidColor, and
            // nothing acted on the answer: Export never skipped blurry files,
            // and the only readers were AiClassification (disabled without an
            // API key) and FileStatus. Moving CleanupEvaluation earlier on
            // 2026-09-07 - correct on its own terms - put the one step that
            // could have used the verdict ahead of the step producing it. Work
            // whose result is discarded is exactly what this pipeline is meant
            // not to do.

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