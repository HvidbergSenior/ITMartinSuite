using FluentAssertions;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;

namespace ITMartinFileSorter.Tests.Services;

// The pre-flight that keeps wire-gallery from recreating gallery-web with a
// mount that no longer exists (which took every gallery down 2026-09-17).
[TestFixture]
public class NasDeliveryMountsTests
{
    private const string Compose = """
        services:
          budget-web:
            volumes:
              - /volume1/docker/budget:/data
          gallery-web:
            image: martinsuite-gallery-web
            environment:
              - Galleries__0__Slug=mie
            volumes:
              - /volume1/docker/filesorter/library/mie:/library/mie:ro
              - "/volume1/MartinMusik/Udgivet:/library/koncert:ro"
              - named-vol:/cache
            restart: unless-stopped
          other-web:
            volumes:
              - /volume1/other:/x
        """;

    [Test]
    public void Lists_only_gallery_web_host_paths()
    {
        var mounts = NasDeliveryService.GalleryWebHostMounts(Compose);
        mounts.Should().Equal("/volume1/docker/filesorter/library/mie", "/volume1/MartinMusik/Udgivet");
    }

    [Test]
    public void No_gallery_service_means_no_mounts()
    {
        NasDeliveryService.GalleryWebHostMounts("services:\n  x:\n    volumes:\n      - /a:/b\n").Should().BeEmpty();
    }
}
