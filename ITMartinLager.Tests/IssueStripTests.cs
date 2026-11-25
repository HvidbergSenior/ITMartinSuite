using FluentAssertions;
using ITMartinLager.Infrastructure.External;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ITMartinLager.Tests;

// 2026-11-25 (user: the Stikprøve strip on the Elementals card was "just a black rectangle"): a cover photographed on a black table.
public sealed class IssueStripTests
{
    private static byte[] Jpeg(Image<Rgb24> img)
    {
        using var ms = new MemoryStream();
        img.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
        return ms.ToArray();
    }

    /// <summary>A "cover" with stripes of detail inside a plain margin.</summary>
    private static Image<Rgb24> CoverOn(Rgb24 margin, int pad)
    {
        var img = new Image<Rgb24>(500, 700, margin);
        for (var y = pad; y < 700 - pad; y++)
            for (var x = pad; x < 500 - pad; x++)
                img[x, y] = ((x / 6 + y / 6) % 2 == 0) ? new Rgb24(220, 200, 40) : new Rgb24(120, 30, 90);
        return img;
    }

    [Test]
    public async Task A_cover_on_a_black_table_gets_a_strip_from_the_cover_not_the_black()
    {
        using var img = CoverOn(new Rgb24(0, 0, 0), pad: 120);
        var strip = await new FilePhotoStore(Path.GetTempPath()).IssueStripAsync(Jpeg(img), default);

        strip.Should().NotBeNull();
        using var cut = Image.Load<Rgb24>(strip!);
        var dark = 0;
        for (var y = 0; y < cut.Height; y++)
            for (var x = 0; x < cut.Width; x++) if (cut[x, y].R < 20 && cut[x, y].G < 20) dark++;
        dark.Should().BeLessThan(cut.Width * cut.Height / 10, "the plain black margin is trimmed away");
    }

    [Test]
    public async Task A_plain_photo_has_nothing_to_read_so_no_strip()
    {
        using var img = new Image<Rgb24>(500, 700, new Rgb24(0, 0, 0));
        (await new FilePhotoStore(Path.GetTempPath()).IssueStripAsync(Jpeg(img), default)).Should().BeNull();
    }
}
