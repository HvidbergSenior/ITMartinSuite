using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartinFileSorter.Tests.Package1Tests;

// Documents (and enforces) the exact, precise order Package1 actually runs its
// steps in - the single most-asked "wait, what order does the sort really go
// in?" question, now answered by a test instead of by re-reading source every
// time. Built 2026-08-20 after repeated manual reorganization sessions on real
// customer libraries (Rico, Mie) made clear nobody had a reliable, current
// answer without re-deriving it from scratch each session.
//
// Deliberately constructs the REAL Package1WorkflowDefinition and reads its
// actual Steps property, rather than just inspecting constructor parameter
// order - the two are not the same thing. Proven necessary while writing this
// test: ThumbnailWorkflowStep is a real constructor parameter that is
// deliberately commented out of the Steps array (superseded by
// GalleryThumbnailWorkflowStep, which runs post-export instead of pre-export -
// see the comment in Package1WorkflowDefinition.cs). A parameter-order-only
// test would have silently asserted a step that never actually runs.
//
// Each constructor argument is an uninitialized instance of the real step
// type (RuntimeHelpers.GetUninitializedObject - bypasses the constructor and
// all its real dependencies, which would otherwise require a full DI graph:
// Claude API client, DB context, ffprobe, etc.). This is safe here because
// Package1WorkflowDefinition's constructor only stores references into an
// array - it never calls any member on them - so the objects only need a
// correct runtime Type, never working behavior.
[TestFixture]
public class Package1StepOrderTests
{
    // The authoritative order, one line per step, in the exact sequence
    // Package1 actually executes them (i.e. what's really in Steps, not just
    // what's accepted by the constructor). If this list and the real Steps
    // array ever disagree, one of them is wrong - update whichever one
    // doesn't match the *actual intended* pipeline order, don't just make the
    // test pass.
    private static readonly string[] ExpectedStepOrder =
    [
        "DvdJoinWorkflowStep",              // 1.  Joins split DVD-rip video segments back into whole files, if any.
        "FileDiscoveryWorkflowStep",        // 2.  Walks the source tree, builds the initial MediaFile list.
        "MediaRulesWorkflowStep",           // 3.  Extension/codec-based type classification (incl. real .mp4 codec check).
        "LivePhotoDetectionWorkflowStep",   // 4.  Pairs iPhone Live Photo stills with their motion-clip video.
        "HashWorkflowStep",                 // 5.  Computes each file's content hash.
        "MetadataWorkflowStep",             // 6.  Reads EXIF/video metadata - date, GPS, camera model, etc.
        "DuplicateDetectionWorkflowStep",   // 7.  Exact-hash + perceptual-hash image duplicate grouping.
        "AudioDuplicateDetectionWorkflowStep", // 8.  Same idea, scoped to audio tracks.
        "ImageNormalizationWorkflowStep",   // 9.  HEIC/HEIF/AVIF -> JPG conversion, orientation baking (now on every image).
        "ImageQualityWorkflowStep",         // 10. Free local blur/solid-color check on every image.
        "VideoNormalizationWorkflowStep",   // 11. Container/codec normalization for web-safe playback.
        "CleanupEvaluationWorkflowStep",    // 12. Decides Keep/Delete/Review per file (junk, near-duplicates).
        "AiClassificationWorkflowStep",     // 13. Optional Claude-based classification (EnableAiClassification).
        "Manifest1BuildWorkflowStep",       // 14. Builds the in-memory Package1 manifest from everything above.
        "ExportWorkflowExecutionStep",      // 15. Physically copies files into the final category/Year/group layout.
        "GalleryThumbnailWorkflowStep",     // 16. Generates the gallery's own per-file thumbnails, post-export.
        "FileStatusWorkflowStep",           // 17. Writes filestatus.json - needs ExportedPath/category already settled.
        // NOTE: VideoSegmentationWorkflowStep, SegmentThumbnailWorkflowStep, and
        // ThumbnailWorkflowStep were removed 2026-08-24 - all three were
        // permanently dead code (no UI/request field ever set EnableSegmentation
        // to true, so segmentation and its thumbnail step could never run;
        // ThumbnailWorkflowStep was already commented out of the real Steps
        // array, superseded by GalleryThumbnailWorkflowStep).
    ];

    [Test]
    public void Package1_step_order_matches_the_documented_sequence()
    {
        var ctor = typeof(Package1WorkflowDefinition)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single();

        var args = ctor.GetParameters()
            .Select(p => RuntimeHelpers.GetUninitializedObject(p.ParameterType))
            .ToArray();

        var definition = (Package1WorkflowDefinition)ctor.Invoke(args);

        var actualOrder = definition.Steps
            .Select(s => s.GetType().Name)
            .ToArray();

        actualOrder.Should().Equal(ExpectedStepOrder,
            "Package1WorkflowDefinition's real Steps array has changed - update ExpectedStepOrder " +
            "above to match the new intended order, or fix the reorder/removal if it was accidental.");
    }

    [Test]
    public void Every_step_in_Steps_implements_IWorkflowStep()
    {
        var ctor = typeof(Package1WorkflowDefinition)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single();

        var args = ctor.GetParameters()
            .Select(p => RuntimeHelpers.GetUninitializedObject(p.ParameterType))
            .ToArray();

        var definition = (Package1WorkflowDefinition)ctor.Invoke(args);

        definition.Steps.Should().AllBeAssignableTo<IWorkflowStep>();
        definition.Steps.Should().HaveCount(ExpectedStepOrder.Length);
    }
}
