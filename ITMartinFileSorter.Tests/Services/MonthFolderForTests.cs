using FluentAssertions;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;

namespace ITMartinFileSorter.Tests.Services;

// A re-dated file joins the month folder its year already uses - single
// months, ranges, or a new Danish "N Måned" only when nothing covers it.
[TestFixture]
public class MonthFolderForTests
{
    private string _year = "";

    [SetUp]
    public void SetUp()
    {
        _year = Path.Combine(Path.GetTempPath(), "MonthFolder_" + Guid.NewGuid().ToString("N"), "2013");
        Directory.CreateDirectory(_year);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(Path.GetDirectoryName(_year)!, recursive: true); } catch { /* best effort */ }
    }

    [Test]
    public void Uses_the_existing_single_month_folder()
    {
        Directory.CreateDirectory(Path.Combine(_year, "4 Juni"));
        Directory.CreateDirectory(Path.Combine(_year, "Ukendt måned"));

        LibraryPolishService.MonthFolderFor(_year, 6).Should().Be("4 Juni");
    }

    [Test]
    public void Uses_the_range_folder_that_covers_the_month()
    {
        Directory.CreateDirectory(Path.Combine(_year, "1 Januar-Marts"));
        Directory.CreateDirectory(Path.Combine(_year, "2 September-Oktober"));

        LibraryPolishService.MonthFolderFor(_year, 2).Should().Be("1 Januar-Marts");
        LibraryPolishService.MonthFolderFor(_year, 10).Should().Be("2 September-Oktober");
    }

    [Test]
    public void Creates_a_danish_month_name_when_nothing_covers_it()
    {
        Directory.CreateDirectory(Path.Combine(_year, "1 Januar-Marts"));

        LibraryPolishService.MonthFolderFor(_year, 8).Should().Be("8 August");
        LibraryPolishService.MonthFolderFor(Path.Combine(_year, "nope"), 5).Should().Be("5 Maj");
    }
}
