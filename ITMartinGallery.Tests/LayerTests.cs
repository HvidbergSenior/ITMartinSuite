using FluentAssertions;
using ITMartinGallery.Application;
using ITMartinGallery.Domain;

namespace ITMartinGallery.Tests;

// The DDD/Clean rules for Gallery (2026-10-28): Domain needs nothing, Application only Domain and never the disk
// directly, the Server only wires middleware and endpoints to use cases.
public class LayerTests
{
    private static string Repo => Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..");

    private static string[] Refs(Type t) =>
        t.Assembly.GetReferencedAssemblies().Select(a => a.Name!)
            .Where(n => n.StartsWith("ITMartin") || n.StartsWith("TagLib") || n.StartsWith("MetadataExtractor") ||
                        n.StartsWith("Microsoft.Data") || n.StartsWith("Microsoft.AspNetCore") || n.StartsWith("Microsoft.Extensions"))
            .ToArray();

    [Test]
    public void Domain_has_no_outside_dependencies() => Refs(typeof(GalleryCatalog)).Should().BeEmpty();

    [Test]
    public void Application_depends_only_on_Domain() =>
        Refs(typeof(FolderBrowser)).Should().BeEquivalentTo(["ITMartinGallery.Domain"]);

    [Test]
    public void Application_reaches_the_disk_only_through_its_port()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(Repo, "ITMartinGallery.Application"), "*.cs"))
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("File.", file).And.NotContain("Directory.", file).And.NotContain("FileInfo", file);
        }
    }

    [Test]
    public void Server_has_no_disk_or_database_code()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(Repo, "ITMartinFileSorter.Gallery.Server"), "*.cs"))
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("Directory.", file).And.NotContain("File.Exists", file).And.NotContain("Sqlite", file)
                .And.NotContain("TagLib", file).And.NotContain("ITMartin.Media", file);
        }
    }
}
