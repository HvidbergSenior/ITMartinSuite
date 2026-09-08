using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartinFileSorter.Tests.QuickSortTests;

[TestFixture]
public class CategoryHelperNeverNecessaryTests
{
    private static MediaFile FileWithSubCategory(MediaSubCategory subCategory) =>
        new("/jobs/test/file.jpg", DateTime.UtcNow, MediaType.Image, 3) { SubCategory = subCategory };

    [TestCase(MediaSubCategory.Screenshot)]
    [TestCase(MediaSubCategory.Meme)]
    [TestCase(MediaSubCategory.Gif)]
    [TestCase(MediaSubCategory.Chat)]
    [TestCase(MediaSubCategory.Movie)]
    public void Never_necessary_categories_are_flagged(MediaSubCategory subCategory)
    {
        CategoryHelper.IsNeverNecessary(FileWithSubCategory(subCategory)).Should().BeTrue();
    }

    [Test]
    public void An_ordinary_photo_is_not_flagged()
    {
        var file = new MediaFile("/jobs/test/IMG_1234.jpg", DateTime.UtcNow, MediaType.Image, 3);

        CategoryHelper.IsNeverNecessary(file).Should().BeFalse();
    }

    [Test]
    public void Music_is_not_flagged()
    {
        var file = new MediaFile("/jobs/test/song.mp3", DateTime.UtcNow, MediaType.Audio, 3);

        CategoryHelper.IsNeverNecessary(file).Should().BeFalse();
    }
}
