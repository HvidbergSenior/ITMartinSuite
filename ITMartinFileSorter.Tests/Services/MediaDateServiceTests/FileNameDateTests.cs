using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Infrastructure.Metadata;
using Moq;

namespace ITMartinFileSorter.Tests.Services.MediaDateServiceTests;

// The file name is the first date source and the most common one for phone
// photos that lost their EXIF (resized copies, WhatsApp exports). Until
// 2026-09-12 the compact Samsung/Android form was not parsed, and 1,597
// ToshibaTest photos ended up on 1 January of a year folder instead.
[TestFixture]
public class FileNameDateTests
{
    private static MediaDateService Create() =>
        new(Mock.Of<IImageMetadataService>(), Mock.Of<IVideoMetadataService>(), Mock.Of<IDocumentMetadataService>());

    [TestCase(@"C:\x\20200819_083325.jpg", 2020, 8, 19, 8, 33, 25)]
    [TestCase(@"C:\x\IMG_20160920_143320.jpeg", 2016, 9, 20, 14, 33, 20)]
    [TestCase(@"C:\x\Resized 20200819 083325 7241.jpeg", 2020, 8, 19, 8, 33, 25)]
    [TestCase(@"C:\x\20190430_164632.3gp", 2019, 4, 30, 16, 46, 32)]
    [TestCase(@"C:\x\2020-08-19_08-33-25.jpg", 2020, 8, 19, 8, 33, 25)]
    [TestCase(@"C:\x\2020-08-19 something.jpg", 2020, 8, 19, 0, 0, 0)]
    public void Reads_the_date_out_of_the_file_name(string path, int y, int mo, int d, int h, int mi, int s)
    {
        var r = Create().GetBestDate(new MediaDateRequest(path));

        r.Source.Should().Be("Filename");
        r.IsReliable.Should().BeTrue();
        r.Date.Should().Be(new DateTime(y, mo, d, h, mi, s));
    }

    [TestCase(@"C:\x\IMG_4683.jpeg")]
    [TestCase(@"C:\x\P7213530.jpg")]
    [TestCase(@"C:\x\12345678901234.jpg", Description = "fourteen digits that are not a date")]
    [TestCase(@"C:\x\20230231_120000.jpg", Description = "31 February")]
    public void Names_without_a_real_date_are_not_dated_from_the_name(string path)
    {
        var r = Create().GetBestDate(new MediaDateRequest(path));

        r.Source.Should().NotBe("Filename");
    }
}
