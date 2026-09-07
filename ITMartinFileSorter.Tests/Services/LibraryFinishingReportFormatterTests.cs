using FluentAssertions;
using ITMartin.Media.Application.Pipelines.LibraryFinishing;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartinFileSorter.Tests.Services;

// "make a description that I can read after run... Overview and analysis"
// (2026-09-07) - this is that description. Pure formatting, no filesystem or
// service calls, so every case here is a hand-built report in, Markdown
// string out.
[TestFixture]
public class LibraryFinishingReportFormatterTests
{
    private static LibraryFinishingReport MinimalReport() => new()
    {
        LibraryPath = @"D:\ToshibaTest",
        StartedAtUtc = new DateTime(2026, 9, 7, 22, 0, 0, DateTimeKind.Utc),
        FinishedAtUtc = new DateTime(2026, 9, 8, 1, 30, 0, DateTimeKind.Utc),
    };

    [Test]
    public void Includes_the_library_path_and_duration()
    {
        var markdown = LibraryFinishingReportFormatter.ToMarkdown(MinimalReport());

        markdown.Should().Contain(@"D:\ToshibaTest");
        markdown.Should().Contain("3t 30m");
    }

    [Test]
    public void Reports_zero_duplicates_deleted_language_around_moved_not_deleted()
    {
        var report = MinimalReport();
        report.CategoryFoldersDeduped.Add("Billeder");
        report.DuplicatesMoved = 42;
        report.FilesCheckedForDuplicates = 1000;

        var markdown = LibraryFinishingReportFormatter.ToMarkdown(report);

        markdown.Should().Contain("42 dubletter flyttet til Dubletter/");
        markdown.Should().Contain("intet er slettet");
    }

    [Test]
    public void Surfaces_phase_errors_prominently_when_present()
    {
        var report = MinimalReport();
        report.PhaseErrors.Add("Dedup: disk full");

        var markdown = LibraryFinishingReportFormatter.ToMarkdown(report);

        markdown.Should().Contain("⚠ Fejl undervejs");
        markdown.Should().Contain("Dedup: disk full");
    }

    [Test]
    public void Omits_the_error_section_entirely_when_everything_succeeded()
    {
        var markdown = LibraryFinishingReportFormatter.ToMarkdown(MinimalReport());

        markdown.Should().NotContain("Fejl undervejs");
    }

    [Test]
    public void Lists_yearbook_years_generated()
    {
        var report = MinimalReport();
        report.Yearbooks.Add(new YearbookResult { Year = 2020, PhotoCount = 40, FolderPath = "x", HtmlPath = "x.html" });
        report.Yearbooks.Add(new YearbookResult { Year = 2023, PhotoCount = 12, FolderPath = "y", HtmlPath = "y.html" });

        var markdown = LibraryFinishingReportFormatter.ToMarkdown(report);

        markdown.Should().Contain("Årbøger genereret: 2");
        markdown.Should().Contain("2020");
        markdown.Should().Contain("2023");
    }
}
