using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartinFileSorter.Tests.QuickSortTests;

// Documents (and enforces) the exact, precise order QuickSort actually runs its
// steps in - the single most-asked "wait, what order does the sort really go
// in?" question, now answered by a test instead of by re-reading source every
// time. Built 2026-08-20 after repeated manual reorganization sessions on real
// customer libraries (Rico, Mie) made clear nobody had a reliable, current
// answer without re-deriving it from scratch each session.
//
// Deliberately constructs the REAL QuickSortWorkflowDefinition and reads its
// actual Steps property, rather than just inspecting constructor parameter
// order - the two are not the same thing. Proven necessary while writing this
// test: ThumbnailWorkflowStep is a real constructor parameter that is
// deliberately commented out of the Steps array (superseded by
// GalleryThumbnailWorkflowStep, which runs post-export instead of pre-export -
// see the comment in QuickSortWorkflowDefinition.cs). A parameter-order-only
// test would have silently asserted a step that never actually runs.
//
// Each constructor argument is an uninitialized instance of the real step
// type (RuntimeHelpers.GetUninitializedObject - bypasses the constructor and
// all its real dependencies, which would otherwise require a full DI graph:
// Claude API client, DB context, ffprobe, etc.). This is safe here because
// QuickSortWorkflowDefinition's constructor only stores references into an
// array - it never calls any member on them - so the objects only need a
// correct runtime Type, never working behavior.
[TestFixture]
public class QuickSortStepOrderTests
{
    // The authoritative order, one line per step, in the exact sequence
    // QuickSort actually executes them (i.e. what's really in Steps, not just
    // what's accepted by the constructor). If this list and the real Steps
    // array ever disagree, one of them is wrong - update whichever one
    // doesn't match the *actual intended* pipeline order, don't just make the
    // test pass.
    private static readonly string[] ExpectedStepOrder =
    [
        "CleanStartWorkflowStep",           // 1.  Strips a prior run's generated artifacts (_Galleri, SmartFolders, .packageN, manifest/collections/filestatus.json) so a re-copied already-sorted library scans as clean raw input.
        "DvdJoinWorkflowStep",              // 2.  Joins split DVD-rip video segments back into whole files, if any.
        "FileDiscoveryWorkflowStep",        // 3.  Walks the source tree, builds the initial MediaFile list.
        "MediaRulesWorkflowStep",           // 4.  Extension/codec-based type classification (incl. real .mp4 codec check).
        "LivePhotoDetectionWorkflowStep",   // 5.  Pairs iPhone Live Photo stills with their motion-clip video.
        "HashWorkflowStep",                 // 6.  Computes each file's content hash.
        "MetadataWorkflowStep",             // 7.  Reads EXIF/video metadata - date, GPS, camera model, etc.
        "DuplicateDetectionWorkflowStep",   // 8.  Exact-hash + perceptual-hash image duplicate grouping.
        "AudioDuplicateDetectionWorkflowStep", // 9.  Same idea, scoped to audio tracks.
        "ImageNormalizationWorkflowStep",   // 10. HEIC/HEIF/AVIF -> JPG conversion, orientation baking (now on every image).
        "ImageQualityWorkflowStep",         // 11. Free local blur/solid-color check on every image.
        "CleanupEvaluationWorkflowStep",    // 12. Decides Keep/Delete/Review per file (junk, near-duplicates).
        "AiClassificationWorkflowStep",     // 13. Optional Claude-based classification (EnableAiClassification).
        "Manifest1BuildWorkflowStep",       // 14. Builds the in-memory QuickSort manifest from everything above.
        "ExportWorkflowExecutionStep",      // 15. Physically copies files into the final category/Year/group layout.
        "VideoConvertFinalizeWorkflowStep", // 16. Swaps in any video conversions (dispatched back in step 4, MediaRulesWorkflowStep) that finished after Export ran - fire-and-forget, doesn't block QuickSort's own completion.
        "GalleryThumbnailWorkflowStep",     // 17. Generates the gallery's own per-file thumbnails, post-export.
        "FileStatusWorkflowStep",           // 18. Writes filestatus.json - needs ExportedPath/category already settled.
        // NOTE: VideoSegmentationWorkflowStep, SegmentThumbnailWorkflowStep, and
        // ThumbnailWorkflowStep were removed 2026-08-24 - all three were
        // permanently dead code (no UI/request field ever set EnableSegmentation
        // to true, so segmentation and its thumbnail step could never run;
        // ThumbnailWorkflowStep was already commented out of the real Steps
        // array, superseded by GalleryThumbnailWorkflowStep).
        //
        // VideoNormalizationWorkflowStep was removed 2026-09-03 - QuickSort no
        // longer waits for a dedicated video-conversion step at all.
        // MediaRulesWorkflowStep (step 4) now dispatches each video's
        // conversion via IConcurrentVideoDispatcher the instant it's
        // classified, so conversion runs concurrently with every step below
        // instead of blocking the pipeline. VideoConvertFinalizeWorkflowStep
        // (step 16) is what's left of it - not a conversion step itself, just
        // the swap-in for whatever hadn't finished by the time Export ran.
    ];

    [Test]
    public void QuickSort_step_order_matches_the_documented_sequence()
    {
        var ctor = typeof(QuickSortWorkflowDefinition)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single();

        var args = ctor.GetParameters()
            .Select(p => RuntimeHelpers.GetUninitializedObject(p.ParameterType))
            .ToArray();

        var definition = (QuickSortWorkflowDefinition)ctor.Invoke(args);

        var actualOrder = definition.Steps
            .Select(s => s.GetType().Name)
            .ToArray();

        actualOrder.Should().Equal(ExpectedStepOrder,
            "QuickSortWorkflowDefinition's real Steps array has changed - update ExpectedStepOrder " +
            "above to match the new intended order, or fix the reorder/removal if it was accidental.");
    }

    [Test]
    public void Every_step_in_Steps_implements_IWorkflowStep()
    {
        var ctor = typeof(QuickSortWorkflowDefinition)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single();

        var args = ctor.GetParameters()
            .Select(p => RuntimeHelpers.GetUninitializedObject(p.ParameterType))
            .ToArray();

        var definition = (QuickSortWorkflowDefinition)ctor.Invoke(args);

        definition.Steps.Should().AllBeAssignableTo<IWorkflowStep>();
        definition.Steps.Should().HaveCount(ExpectedStepOrder.Length);
    }
}
