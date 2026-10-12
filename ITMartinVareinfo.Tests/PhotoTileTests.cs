using FluentAssertions;
using ITMartinVareinfo.Infrastructure.External;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ITMartinVareinfo.Tests;

// Close-ups for reading a pile of ~10 books (user 2026-10-12: titles were poor from one shrunk photo).
public class PhotoTileTests
{
    private static async Task<MemoryStream> Jpeg(int w, int h)
    {
        using var img = new Image<Rgb24>(w, h);
        var ms = new MemoryStream();
        await img.SaveAsJpegAsync(ms);
        ms.Position = 0;
        return ms;
    }

    [Test]
    public async Task A_phone_photo_gives_four_overlapping_close_ups_no_bigger_than_what_Claude_reads()
    {
        var store = new FilePhotoStore(Path.GetTempPath());
        var tiles = await store.TilesAsync(await Jpeg(4032, 3024), CancellationToken.None);

        tiles.Should().HaveCount(4);
        foreach (var t in tiles)
        {
            var info = Image.Identify(t);
            Math.Max(info.Width, info.Height).Should().BeLessThanOrEqualTo(FilePhotoStore.MaxEdge);
            info.Width.Should().BeGreaterThan(1400, "a close-up must hold more detail than the whole photo shrunk");
        }
    }

    [Test]
    public async Task A_small_photo_gives_no_close_ups()
    {
        var store = new FilePhotoStore(Path.GetTempPath());
        (await store.TilesAsync(await Jpeg(1600, 1200), CancellationToken.None)).Should().BeEmpty();
    }
}
