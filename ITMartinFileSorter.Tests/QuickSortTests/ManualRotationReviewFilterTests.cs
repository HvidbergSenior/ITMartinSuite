using FluentAssertions;
using ITMartin.Media.Application.Pipelines.QuickSort.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartinFileSorter.Tests.QuickSortTests;

// Which photos get staged for a human to rotate by hand.
//
// This filter was wrong once, badly: keying on "no EXIF orientation tag"
// alone flagged 10,511 of 16,990 photos on the ToshibaTest library - 62%,
// which is not a shortlist, it is the whole library. The noise was entirely
// modern (824 from 2025, 2,232 undated loose files), none of it from a
// camera that has ever produced a sideways photo.
//
// What actually predicted a rotated photo was the CAMERA: every one of the
// ~600 rotations found by hand came from two Olympus bodies used 2007-2010.
// These tests pin that lesson so the broad rule cannot come back.
[TestFixture]
public class ManualRotationReviewFilterTests
{
    private static MediaFile Photo(int? year, bool orientationKnown, bool unreliableCamera)
    {
        var file = new MediaFile("C:\\x\\photo.jpg", null, MediaType.Image, 1024);
        if (year is not null) file.SetDate(new DateTime(year.Value, 6, 1), isReliable: true);
        file.OrientationKnownFromExif = orientationKnown;
        file.OrientationSourceIsUnreliable = unreliableCamera;
        return file;
    }

    [Test]
    public void A_known_bad_camera_is_always_flagged_whatever_the_year()
    {
        FileStatusWorkflowStep.NeedsManualRotationReview(
            Photo(2008, orientationKnown: false, unreliableCamera: true))
            .Should().BeTrue();

        // Even with a tag present, and even on a modern date - the camera
        // writing the tag is the thing that cannot be trusted.
        FileStatusWorkflowStep.NeedsManualRotationReview(
            Photo(2024, orientationKnown: true, unreliableCamera: true))
            .Should().BeTrue();
    }

    [Test]
    public void A_readable_orientation_tag_means_it_is_already_handled()
    {
        FileStatusWorkflowStep.NeedsManualRotationReview(
            Photo(2008, orientationKnown: true, unreliableCamera: false))
            .Should().BeFalse();
    }

    [Test]
    public void A_missing_tag_counts_only_on_a_pre_2011_photo()
    {
        FileStatusWorkflowStep.NeedsManualRotationReview(
            Photo(2009, orientationKnown: false, unreliableCamera: false))
            .Should().BeTrue();

        FileStatusWorkflowStep.NeedsManualRotationReview(
            Photo(2010, orientationKnown: false, unreliableCamera: false))
            .Should().BeTrue();
    }

    // The 824 photos from 2025 that the broad rule swept in.
    [Test]
    public void A_modern_photo_with_no_tag_is_not_flagged()
    {
        FileStatusWorkflowStep.NeedsManualRotationReview(
            Photo(2011, orientationKnown: false, unreliableCamera: false))
            .Should().BeFalse();

        FileStatusWorkflowStep.NeedsManualRotationReview(
            Photo(2025, orientationKnown: false, unreliableCamera: false))
            .Should().BeFalse();
    }

    // The 2,232 loose-root files - screenshots, downloads, Snapchat saves.
    // No date to judge the era by, so leave them alone rather than sweep in
    // every undated image in the library.
    [Test]
    public void An_undated_photo_with_no_tag_is_not_flagged()
    {
        FileStatusWorkflowStep.NeedsManualRotationReview(
            Photo(null, orientationKnown: false, unreliableCamera: false))
            .Should().BeFalse();
    }
}
