using FluentAssertions;
using ITMartinPolstrer.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ITMartinPolstrer.Tests.Services;

[TestFixture]
public class MediaStoreTests
{
    private string _root = "";
    private MediaStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "polstrer-test-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Polstrer:MediaRoot"] = _root })
            .Build();
        _store = new MediaStore(config, NullLogger<MediaStore>.Instance);
    }

    [TearDown]
    public void TearDown() { try { Directory.Delete(_root, true); } catch { } }

    [Test]
    public async Task Photo_is_stored_under_the_piece_folder_with_a_thumbnail_no_wider_than_480()
    {
        using var ms = new MemoryStream();
        using (var img = new Image<Rgba32>(1600, 1200)) await img.SaveAsJpegAsync(ms);
        ms.Position = 0;

        var m = await _store.SaveAsync("stol-abc123", new MediaStore.Pending("IMG_0001.JPG", ms));

        m.IsVideo.Should().BeFalse();
        m.RelativePath.Should().StartWith("stol-abc123/").And.EndWith(".jpg");
        File.Exists(Path.Combine(_root, m.RelativePath)).Should().BeTrue();
        m.ThumbPath.Should().NotBeNull();
        using var thumb = await Image.LoadAsync(Path.Combine(_root, m.ThumbPath!));
        thumb.Width.Should().Be(480);
        thumb.Height.Should().Be(360);
    }

    [Test]
    public async Task Video_is_stored_without_a_thumbnail()
    {
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var m = await _store.SaveAsync("stol-abc123", new MediaStore.Pending("clip.MOV", ms));

        m.IsVideo.Should().BeTrue();
        m.ThumbPath.Should().BeNull();
        m.RelativePath.Should().EndWith(".mov");
    }

    [Test]
    public async Task Delete_removes_original_and_thumbnail()
    {
        using var ms = new MemoryStream();
        using (var img = new Image<Rgba32>(100, 100)) await img.SaveAsJpegAsync(ms);
        ms.Position = 0;
        var m = await _store.SaveAsync("x", new MediaStore.Pending("a.jpg", ms));

        _store.Delete(m);

        File.Exists(Path.Combine(_root, m.RelativePath)).Should().BeFalse();
        File.Exists(Path.Combine(_root, m.ThumbPath!)).Should().BeFalse();
    }
}
