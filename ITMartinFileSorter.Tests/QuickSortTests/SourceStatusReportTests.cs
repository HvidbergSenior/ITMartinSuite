using FluentAssertions;
using ITMartin.Media.Application.Pipelines.QuickSort.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartinFileSorter.Tests.QuickSortTests;

// _kilder.json (user 2026-10-19): every uploaded file gets a state the customer can see.
public class SourceStatusReportTests
{
    private const string Root = "/jobs/annette-kent";
    private const string Lib = "/library/annette-kent";

    private static MediaFile File(string name) =>
        new($"{Root}/{name}", new DateTime(2026, 5, 16), MediaType.Image, 1000, isDateReliable: true);

    [Test]
    public void Each_source_file_gets_its_state()
    {
        var sorted = File("IMG_0229_519122.jpeg");
        sorted.CleanupDecision = CleanupDecision.Keep;
        sorted.ExportedPath = $"{Lib}/Billeder/2026/IMG 0229 519122.jpeg";

        var copy = File("IMG_0229 kopi.jpeg");
        copy.CleanupDecision = CleanupDecision.Delete;

        var broken = File("IMG_0257_519091.jpeg");
        broken.Failed = true;

        var groups = new List<DuplicateGroup> { new() { Hash = "h", Files = [sorted, copy] } };
        var allFiles = new[] { sorted.FullPath, copy.FullPath, broken.FullPath, $"{Root}/icons/like.png" };

        var report = SourceStatusReport.Build(Root, Lib, [sorted, copy, broken], groups, allFiles, DateTimeOffset.UtcNow);

        report.Files.Should().BeEquivalentTo(new[]
        {
            new SourceStatusReport.Entry("icons/like.png", SourceStatusReport.NotIncluded, null),
            new SourceStatusReport.Entry("IMG_0229 kopi.jpeg", SourceStatusReport.Duplicate, null),
            new SourceStatusReport.Entry("IMG_0229_519122.jpeg", SourceStatusReport.Sorted, "Billeder/2026/IMG 0229 519122.jpeg"),
            new SourceStatusReport.Entry("IMG_0257_519091.jpeg", SourceStatusReport.Failed, null),
        });
    }

    [Test]
    public void Deleted_file_outside_a_duplicate_group_is_not_called_a_duplicate()
    {
        var screenshot = File("Screenshot_1.png");
        screenshot.CleanupDecision = CleanupDecision.Delete;

        var report = SourceStatusReport.Build(Root, Lib, [screenshot], [], [screenshot.FullPath], DateTimeOffset.UtcNow);

        report.Files.Single().State.Should().Be(SourceStatusReport.NotIncluded);
    }
}
