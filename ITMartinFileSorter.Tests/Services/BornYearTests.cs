using FluentAssertions;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;
using ITMartin.Media.Runtime.BackgroundJobs;

namespace ITMartinFileSorter.Tests.Services;

// The birth-year rule lives or dies on two small parsers: reading the year out
// of a reference folder name, and reading the year out of a library path. Both
// are the kind of thing that quietly returns null and disables the rule for
// everyone, so they are pinned here. See PersonEntity.BornYear for what the rule
// is for.
[TestFixture]
public class BornYearTests
{
    [TestCase("Theodor (2013)", "Theodor", 2013)]
    [TestCase("Theodor", "Theodor", null)]
    [TestCase("Jørgens Mor", "Jørgens Mor", null)]
    [TestCase("Niels (den lille)", "Niels (den lille)", null)]
    [TestCase("  Maya (2005) ", "Maya", 2005)]
    [TestCase("Anna (1985)", "Anna", 1985)]
    [TestCase("Ole (85)", "Ole (85)", null)]
    public void Folder_name_splits_into_name_and_optional_year(string folder, string name, int? year)
    {
        var (n, y) = QuickSortAddonSteps.SplitNameAndBirthYear(folder);

        n.Should().Be(name);
        y.Should().Be(year);
    }

    [TestCase("/library/ToshibaTest/Billeder/2014/3 Juli/foto.jpg", 2014)]
    [TestCase(@"D:\lib\Billeder\2020\1 Juli-August\IMG_1.jpg", 2020)]
    [TestCase("/library/x/Videoer/2008/klip.mp4", 2008)]
    [TestCase("/library/ToshibaTest/Billeder/Ukendt måned/foto.jpg", null)]
    [TestCase("/library/ToshibaTest/foto.jpg", null)]
    [TestCase("/library/ToshibaTest/Billeder/1999/x.jpg", 1999)]
    public void Year_is_read_from_the_year_folder_in_the_path(string path, int? year)
    {
        SmartFoldersService.YearFromLibraryPath(path).Should().Be(year);
    }
}
