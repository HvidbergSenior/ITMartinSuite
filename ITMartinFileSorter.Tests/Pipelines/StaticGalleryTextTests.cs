using FluentAssertions;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;

namespace ITMartinFileSorter.Tests.Pipelines;

// The static gallery is a Danish page (user 2026-11-18 on the Toshiba drive: "1 filer", English month names).
[TestFixture]
public class StaticGalleryTextTests
{
    [TestCase(1, "1 fil")]
    [TestCase(2, "2 filer")]
    [TestCase(812, "812 filer")]
    public void File_count_is_Danish(int count, string expected) =>
        StaticGalleryExportService.Files(count).Should().Be(expected);

    [Test]
    public void Months_are_Danish() =>
        new DateTime(2002, 1, 13).ToString("d. MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("da-DK"))
            .Should().Be("13. januar 2002");
}
