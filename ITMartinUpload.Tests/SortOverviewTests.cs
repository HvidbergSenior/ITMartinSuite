using FluentAssertions;
using ITMartinUpload.Domain;

namespace ITMartinUpload.Tests;

public class SortOverviewTests
{
    private static readonly DateTimeOffset Run = new(2026, 10, 16, 12, 14, 0, TimeSpan.Zero);

    private static readonly SortReport Report = new(Run,
    [
        new SourceEntry("IMG_0229_519122.jpeg", "Sorteret", "Billeder/2026/IMG 0229 519122.jpeg"),
        new SourceEntry("IMG_0229 kopi.jpeg", "Dublet", null),
        new SourceEntry("Screenshot_1.png", "IkkeMed", null),
        new SourceEntry("IMG_0257_519091.jpeg", "Fejl", null),
    ]);

    [Test]
    public void Files_get_the_state_from_the_last_run()
    {
        var before = Run.UtcDateTime.AddHours(-2);
        var files = SortOverview.Build(
        [
            new UploadedFile("IMG_0229_519122.jpeg", before),
            new UploadedFile("IMG_0229 kopi.jpeg", before),
            new UploadedFile("Screenshot_1.png", before),
            new UploadedFile("IMG_0257_519091.jpeg", before),
        ], Report);

        files.Select(f => f.State).Should().Equal(SortState.Sorted, SortState.Duplicate, SortState.NotIncluded, SortState.Failed);
        files[0].LibraryPath.Should().Be("Billeder/2026/IMG 0229 519122.jpeg");
    }

    [Test]
    public void File_sent_after_the_run_is_waiting_even_if_the_name_is_known()
    {
        var files = SortOverview.Build(
        [
            new UploadedFile("IMG_0229_519122.jpeg", Run.UtcDateTime.AddMinutes(5)),
            new UploadedFile("IMG_9999_new.jpeg", Run.UtcDateTime.AddDays(3)),
        ], Report);

        files.Should().OnlyContain(f => f.State == SortState.Waiting);
    }

    [Test]
    public void Without_a_run_everything_is_waiting_and_the_summary_counts_it()
    {
        var files = SortOverview.Build([new UploadedFile("a.jpg", DateTime.UtcNow), new UploadedFile("b.jpg", DateTime.UtcNow)], null);
        var summary = SortOverview.Summarize(files, null);

        summary.Should().Be(new SortSummary(2, 0, 0, 0, 0, 2, null));
        summary.AnySorted.Should().BeFalse();
    }
}
