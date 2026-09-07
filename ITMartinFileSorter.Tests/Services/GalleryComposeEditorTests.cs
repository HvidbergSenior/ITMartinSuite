using FluentAssertions;
using ITMartin.Media.Application.Pipelines.LibraryFinishing;

namespace ITMartinFileSorter.Tests.Services;

// Fixture below is trimmed directly from the real, live docker-compose.yaml
// on 10.0.0.126 (fetched 2026-09-07) - same indentation, same structure,
// same trailing service (budget-web) to prove the block-boundary detection
// stops in the right place. GalleryComposeEditor never gets to run against
// this actual file without a human confirming the diff first (see
// NasDeliveryService) - these tests are the real safety net for a shared
// production file serving ~30 real apps.
[TestFixture]
public class GalleryComposeEditorTests
{
    private const string RealisticFixture = """
        services:
          gallery-web:
            container_name: gallery-web
            profiles: [manual]

            build:
              context: .
              dockerfile: ./ITMartinFileSorter.Gallery.Server/Dockerfile

            env_file:
              - magic.env

            ports:
              - "8092:8080"

            environment:
              - ASPNETCORE_URLS=http://+:8080
              - Galleries__0__Slug=mie
              - Galleries__0__Name=Mie
              - Galleries__0__Path=/library/mie
              - Galleries__0__Password=8670Låsby
              - Galleries__0__ShowSummary=true
              - Galleries__1__Slug=rico
              - Galleries__1__Name=Rico
              - Galleries__1__Path=/library/rico
              - Galleries__1__Password=Bogshoppen1
              - Galleries__1__ShowSummary=true

            volumes:
              - /volume1/docker/filesorter/library/mie:/library/mie:ro
              - /volume1/docker/filesorter/library/rico:/library/rico:ro

            restart: unless-stopped

            networks:
              - martinnet

          budget-web:
            container_name: budget-web
            profiles: [manual]

            environment:
              - ASPNETCORE_URLS=http://+:8080

            restart: unless-stopped
        """;

    private const string NoGalleriesYetFixture = """
        services:
          gallery-web:
            container_name: gallery-web

            environment:
              - ASPNETCORE_URLS=http://+:8080

            volumes:
              - /volume1/docker/filesorter/library/mie:/library/mie:ro

            restart: unless-stopped

          budget-web:
            container_name: budget-web
        """;

    [Test]
    public void Appends_a_new_gallery_at_the_next_available_index()
    {
        var result = GalleryComposeEditor.AddGallery(
            RealisticFixture, "toshibatest", "ToshibaTest", "/library/toshibatest",
            "/volume1/docker/filesorter/library/toshibatest", "hunter2");

        result.AlreadyWired.Should().BeFalse();
        result.AssignedIndex.Should().Be(2);
        result.Yaml.Should().Contain("Galleries__2__Slug=toshibatest");
        result.Yaml.Should().Contain("Galleries__2__Name=ToshibaTest");
        result.Yaml.Should().Contain("Galleries__2__Path=/library/toshibatest");
        result.Yaml.Should().Contain("Galleries__2__Password=hunter2");
        result.Yaml.Should().Contain("Galleries__2__ShowSummary=true");
        result.Yaml.Should().Contain("/volume1/docker/filesorter/library/toshibatest:/library/toshibatest:ro");
    }

    [Test]
    public void Never_touches_the_next_services_block()
    {
        var result = GalleryComposeEditor.AddGallery(
            RealisticFixture, "toshibatest", "ToshibaTest", "/library/toshibatest",
            "/volume1/docker/filesorter/library/toshibatest", "hunter2");

        // budget-web's own environment/ASPNETCORE_URLS line must stay
        // exactly once - a boundary bug would insert the new gallery lines
        // into (or after) the wrong service.
        result.Yaml.Should().Contain("budget-web:");
        var budgetSection = result.Yaml[result.Yaml.IndexOf("budget-web:", StringComparison.Ordinal)..];
        budgetSection.Should().NotContain("Galleries__");
    }

    [Test]
    public void Preserves_every_existing_gallery_untouched()
    {
        var result = GalleryComposeEditor.AddGallery(
            RealisticFixture, "toshibatest", "ToshibaTest", "/library/toshibatest",
            "/volume1/docker/filesorter/library/toshibatest", "hunter2");

        result.Yaml.Should().Contain("Galleries__0__Slug=mie");
        result.Yaml.Should().Contain("Galleries__0__Password=8670Låsby");
        result.Yaml.Should().Contain("Galleries__1__Slug=rico");
        result.Yaml.Should().Contain("/volume1/docker/filesorter/library/mie:/library/mie:ro");
        result.Yaml.Should().Contain("/volume1/docker/filesorter/library/rico:/library/rico:ro");
    }

    [Test]
    public void Is_idempotent_a_second_call_for_the_same_slug_changes_nothing()
    {
        var first = GalleryComposeEditor.AddGallery(
            RealisticFixture, "toshibatest", "ToshibaTest", "/library/toshibatest",
            "/volume1/docker/filesorter/library/toshibatest", "hunter2");

        var second = GalleryComposeEditor.AddGallery(
            first.Yaml, "toshibatest", "ToshibaTest", "/library/toshibatest",
            "/volume1/docker/filesorter/library/toshibatest", "hunter2");

        second.AlreadyWired.Should().BeTrue();
        second.Yaml.Should().Be(first.Yaml);
        // Only one Galleries__2__ block, not two.
        System.Text.RegularExpressions.Regex.Matches(second.Yaml, "Galleries__2__Slug=").Should().HaveCount(1);
    }

    [Test]
    public void Handles_the_zero_galleries_case_by_inserting_right_after_ASPNETCORE_URLS()
    {
        var result = GalleryComposeEditor.AddGallery(
            NoGalleriesYetFixture, "first", "First Library", "/library/first",
            "/volume1/docker/filesorter/library/first", "pw");

        result.AssignedIndex.Should().Be(0);
        result.Yaml.Should().Contain("Galleries__0__Slug=first");
        result.Yaml.Should().Contain("/volume1/docker/filesorter/library/first:/library/first:ro");
    }

    [Test]
    public void Throws_a_clear_error_when_gallery_web_service_is_missing()
    {
        var act = () => GalleryComposeEditor.AddGallery(
            "services:\n  budget-web:\n    container_name: budget-web\n",
            "x", "X", "/library/x", "/volume1/docker/filesorter/library/x", "pw");

        act.Should().Throw<InvalidOperationException>().WithMessage("*gallery-web*");
    }
}
