using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartinPolstrer.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinPolstrer.Tests.Services;

// Covers the staging/push logic in isolation - INasDeliveryService itself
// (the real ssh/scp/tar work) is mocked, same convention as
// QuickSortAutoFinishHostedServiceTests uses for the same interface.
[TestFixture]
public class NasPhotoServiceTests
{
    private static (NasPhotoService Service, Mock<INasDeliveryService> Mock) CreateHappyPath()
    {
        var mock = new Mock<INasDeliveryService>();
        mock.Setup(n => n.PushToNasAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NasPushResult { Success = true, RemotePath = "/volume1/docker/filesorter/library/polstrer" });
        mock.Setup(n => n.WireGalleryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryWireResult { Success = true });

        return (new NasPhotoService(mock.Object, NullLogger<NasPhotoService>.Instance), mock);
    }

    [Test]
    public async Task Pushes_once_for_the_whole_batch_not_once_per_file()
    {
        var (service, mock) = CreateHappyPath();
        var files = new List<NasPhotoService.PendingFile>
        {
            new("a.jpg", new MemoryStream("a"u8.ToArray())),
            new("b.jpg", new MemoryStream("b"u8.ToArray())),
            new("c.jpg", new MemoryStream("c"u8.ToArray())),
        };

        var error = await service.UploadAsync("laenestol-abc123", files);

        error.Should().BeNull();
        mock.Verify(n => n.PushToNasAsync(It.IsAny<string>(), "polstrer", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Wires_the_gallery_with_the_job_slug_agnostic_shared_gallery_name()
    {
        var (service, mock) = CreateHappyPath();
        var files = new List<NasPhotoService.PendingFile> { new("a.jpg", new MemoryStream("a"u8.ToArray())) };

        await service.UploadAsync("laenestol-abc123", files);

        mock.Verify(n => n.WireGalleryAsync("polstrer", "Møbelpolstrer", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Returns_the_error_and_does_not_wire_the_gallery_when_push_fails()
    {
        var mock = new Mock<INasDeliveryService>();
        mock.Setup(n => n.PushToNasAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NasPushResult { Success = false, Error = "scp failed" });
        var service = new NasPhotoService(mock.Object, NullLogger<NasPhotoService>.Instance);
        var files = new List<NasPhotoService.PendingFile> { new("a.jpg", new MemoryStream("a"u8.ToArray())) };

        var error = await service.UploadAsync("laenestol-abc123", files);

        error.Should().Be("scp failed");
        mock.Verify(n => n.WireGalleryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Does_nothing_when_no_files_were_selected()
    {
        var (service, mock) = CreateHappyPath();

        var error = await service.UploadAsync("laenestol-abc123", []);

        error.Should().BeNull();
        mock.Verify(n => n.PushToNasAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
